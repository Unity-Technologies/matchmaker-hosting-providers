using System.Net;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Lobby.Model;

namespace LocalServerAllocatorModule;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.AddGameApiClient();
    }
}

/// <summary>
/// Matchmaker allocator that hands matched players a server running on a developer's own machine, instead of
/// provisioning one from a hosting provider.
/// </summary>
/// <remarks>
/// Clients join the session whose id is the match id, but a local server is already running before any match
/// exists. So the server creates a control session (<see cref="LocalServerIds.ToControlSessionId"/>) and watches it;
/// <see cref="Allocate"/> passes the match id to it there, the server creates the match session under that id, and
/// <see cref="Poll"/> waits for the session to publish its network.
/// </remarks>
public class LocalServerAllocator(IGameApiClient gameApiClient, ILogger<LocalServerAllocator> logger) : IMatchmakerAllocator
{
    /// <summary>
    /// Player the allocator joins the control session as. Cloud Code's service identity cannot write lobby-level
    /// data on a lobby it does not host, but any member can write their own player data.
    /// </summary>
    /// <remarks>
    /// Lobby only accepts impersonated player ids shaped like a real player id: 28 alphanumeric characters.
    /// The player doesn't need to exist.
    /// </remarks>
    public const string AllocatorPlayerId = "LocalServerAllocator00000000";

    /// <summary>Player data key, on <see cref="AllocatorPlayerId"/>, carrying the match id for the server.</summary>
    public const string PendingMatchIdKey = "pendingMatchId";

    // Written by the Multiplayer Services SDK's internal network metadata; there is no public API for it yet.
    private const string SessionNetworkDataKey = "_session_network";
    private const int DirectNetwork = 0;
    private const int RelayNetwork = 1;

    /// <summary>Tells the local server which match id to create a session under.</summary>
    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var localServerId = LocalServerIds.FromMatchProperties(request.MatchmakingResults?.MatchProperties);
        if (localServerId is null)
        {
            logger.LogWarning("Match {matchId} has no local server id shared by all players.", request.MatchId);
            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"Every player needs the same valid '{LocalServerIds.PlayerPropertyKey}' player property."
            };
        }

        var controlSessionId = LocalServerIds.ToControlSessionId(localServerId);
        var playerData = new Dictionary<string, PlayerDataObject>
        {
            [PendingMatchIdKey] = new(request.MatchId, PlayerDataObject.VisibilityEnum.Member)
        };

        try
        {
            try
            {
                await gameApiClient.Lobby.JoinLobbyByIdAsync(context, context.ServiceToken, controlSessionId,
                    impersonatedUserId: AllocatorPlayerId,
                    player: new Player(id: AllocatorPlayerId, data: playerData));
            }
            catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
            {
                // Already a member from an earlier match on this server.
                await gameApiClient.Lobby.UpdatePlayerAsync(context, context.ServiceToken, controlSessionId,
                    AllocatorPlayerId, impersonatedUserId: AllocatorPlayerId,
                    playerUpdateRequest: new PlayerUpdateRequest(data: playerData));
            }
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("No control session {sessionId} for match {matchId}; is the local server running?",
                controlSessionId, request.MatchId);
            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"No local server is running for '{localServerId}' (control session '{controlSessionId}' " +
                          "not found). Start the server first."
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Signalling control session {sessionId} for match {matchId} failed.", controlSessionId,
                request.MatchId);
            return new AllocateResponse(AllocateStatus.Error) { Message = "Signalling the local server failed." };
        }

        return new AllocateResponse(AllocateStatus.Created)
        {
            AllocationData = new Dictionary<string, object> { ["matchId"] = request.MatchId }
        };
    }

    /// <summary>
    /// Reports the match session's network once the local server has created it: the server address for a direct
    /// network, or a custom assignment for Relay. Pending until then.
    /// </summary>
    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        try
        {
            var lobby = (await gameApiClient.Lobby.GetLobbyAsync(context, context.ServiceToken, request.MatchId))?.Data;
            if (lobby?.Data is null ||
                !lobby.Data.TryGetValue(SessionNetworkDataKey, out var networkData) ||
                string.IsNullOrEmpty(networkData.Value))
            {
                return new PollResponse(PollStatus.Pending);
            }

            var network = JsonConvert.DeserializeObject<SessionNetwork>(networkData.Value);
            var assignment = network switch
            {
                { Network: DirectNetwork } when !string.IsNullOrEmpty(network.Ip) && network.Port != 0 =>
                    AssignmentData.IpPort(network.Ip, network.Port),
                { Network: RelayNetwork } when !string.IsNullOrEmpty(network.RelayJoinCode) =>
                    AssignmentData.Custom(new Dictionary<string, object>()),
                _ => null
            };

            return assignment is null
                ? new PollResponse(PollStatus.Pending)
                : new PollResponse(PollStatus.Allocated) { AssignmentData = assignment };
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
            // The server has not created the match session yet.
            return new PollResponse(PollStatus.Pending);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Polling match session {matchId} failed.", request.MatchId);
            return new PollResponse(PollStatus.Error) { Message = "Polling the local server's match session failed." };
        }
    }

    // Mirrors the fields this module reads from the SDK's session network metadata.
    private sealed class SessionNetwork
    {
        public int Network { get; set; }
        public string Ip { get; set; } = "";
        public int Port { get; set; }
        public string RelayJoinCode { get; set; } = "";
    }
}
