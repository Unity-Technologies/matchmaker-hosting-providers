using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.MultiplayerModels;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;

namespace PlayFabAllocatorModule;

/// <summary>
/// Module configuration for dependency injection.
/// Registers <see cref="IGameApiClient"/> as a singleton
/// for accessing Unity services like Secret Manager.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddScoped<IPlayFabFactory, PlayFabFactory>();
        config.Dependencies.AddScoped<IPlayFabAuthenticationApi, PlayFabAuthenticationApi>();
    }
}

public class PlayFabAllocator(IGameApiClient gameApiClient, IPlayFabFactory playFabFactory, IPlayFabAuthenticationApi authenticationApi, ILogger<PlayFabAllocator> logger)
    : IMatchmakerAllocator
{
    enum GameServerState
    {
        Initializing = 0,
        StandingBy = 1,
        Active = 2,
        Terminating = 3
    }

    /// <summary>
    /// You will need to set up your PlayFab Build Id.
    /// </summary>
    const string PlayFabBuildId = "MY_BUILD_ID"; // TODO: Replace with your PlayFab Build Id

    /// <summary>
    /// You will need to set up your PlayFab Title Id.
    /// </summary>
    const string PlayFabTitleId = "MY_TITLE_ID"; // TODO: Replace with your PlayFab Title Id

    /// <summary>
    /// You can change the default region as needed.
    /// </summary>
    const string DefaultPlayFabRegion = "EastUs";

    /// <summary>
    /// You will need to set up a secret in the <a
    /// href="https://cloud.unity.com">Unity Dashboard</a> with the
    /// <c>PLAYFAB_SECRET_KEY</c> key containing your PlayFab Secret Key.
    /// </summary>
    const string PlayFabSecretKeySecretName = "PLAYFAB_SECRET_KEY";
    const string AllocationUserFriendlyError = "An error occurred when allocating.";
    const string PollUserFriendlyError = "An error occurred when polling the server status.";
    const string ServerIsTerminatingFriendlyError = "The server is terminating.";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        try
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = (await gameApiClient.SecretManager.GetSecret(context, PlayFabSecretKeySecretName)).Value;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred when retrieving secret for key '{secretName}'.", PlayFabSecretKeySecretName);
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }

        PlayFabSettings.staticSettings.TitleId = PlayFabTitleId;

        var playFabApiSettings = new PlayFabApiSettings { TitleId = PlayFabSettings.staticSettings.TitleId };

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequestResult = await authenticationApi.GetEntityTokenAsync(playFabApiSettings);

            if (!IsValid(entityTokenRequestResult))
            {
                return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred when retrieving the entity token.");
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }

        try
        {
            var authenticationContext = new PlayFabAuthenticationContext
            {
                EntityId = tokenRequestResponse.Entity.Id,
                EntityToken = tokenRequestResponse.EntityToken,
                EntityType = tokenRequestResponse.Entity.Type
            };

            var multiplayerInstanceApi = playFabFactory.CreateMultiplayerInstanceApi(playFabApiSettings, authenticationContext);

            var preferredRegion = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultPlayFabRegion;
            if (string.IsNullOrWhiteSpace(preferredRegion))
            {
                logger.LogError("An error occurred when retrieving the region in matchmaking properties. The region field must not be empty or whitespace.");
                return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
            }

            var multiplayerServerRequest = new RequestMultiplayerServerRequest
            {
                BuildId = PlayFabBuildId,
                PreferredRegions = [preferredRegion],
                SessionId = request.MatchId
            };

            logger.LogDebug("Requesting an allocation for session id: {sessionId}", multiplayerServerRequest.SessionId);

            var allocationResult = await multiplayerInstanceApi.RequestMultiplayerServerAsync(multiplayerServerRequest);

            if (IsValid(allocationResult))
            {
                return new AllocateResponse(AllocateStatus.Created)
                {
                    AllocationData = new Dictionary<string, object>
                    {
                        { "sessionId", allocationResult.Result.SessionId },
                        { "playfabRegion", preferredRegion },
                        { "startTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                        { "matchId", request.MatchId }
                    }
                };
            }

            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred when allocating.");
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        try
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = (await gameApiClient.SecretManager.GetSecret(context, PlayFabSecretKeySecretName)).Value;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occurred when retrieving secret for key '{PlayFabSecretKeySecretName}'.");
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }

        PlayFabSettings.staticSettings.TitleId = PlayFabTitleId;

        var playFabApiSettings = new PlayFabApiSettings { TitleId = PlayFabSettings.staticSettings.TitleId };

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequestResult = await authenticationApi.GetEntityTokenAsync(playFabApiSettings);

            if (!IsValid(entityTokenRequestResult))
            {
                return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred when retrieving the entity token.");
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }

        try
        {
            var authenticationContext = new PlayFabAuthenticationContext
            {
                EntityId = tokenRequestResponse.Entity.Id,
                EntityToken = tokenRequestResponse.EntityToken,
                EntityType = tokenRequestResponse.Entity.Type
            };

            var multiplayerInstanceApi = playFabFactory.CreateMultiplayerInstanceApi(playFabApiSettings, authenticationContext);

            var multiplayerServerDetailsRequest = new GetMultiplayerServerDetailsRequest
            {
                SessionId = request.AllocationData["sessionId"].ToString()
            };

            logger.LogDebug("Requesting details for session id: {sessionId}", multiplayerServerDetailsRequest.SessionId);

            var detailsResult =
                await multiplayerInstanceApi.GetMultiplayerServerDetailsAsync(multiplayerServerDetailsRequest);

            switch (detailsResult)
            {
                case null:
                    logger.LogError("An error occurred when calling {method}. The result is null.", nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync));
                    return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
                case { Result: null }:
                    logger.LogError("An error occurred when calling {method}. Error: {error}.", nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync), SerializeToJson(detailsResult.Error));
                    return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }

            if (!Enum.TryParse<GameServerState>(detailsResult.Result.State, out var serverState))
            {
                logger.LogError("An error occurred when parsing the server state. Server state: {state}", detailsResult.Result.State);
                return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }

            switch (serverState)
            {
                case GameServerState.Initializing:
                case GameServerState.StandingBy:
                    return new PollResponse(PollStatus.Pending);
                case GameServerState.Active:
                    if (!IsValid(detailsResult))
                    {
                        return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
                    }
                    return new PollResponse(PollStatus.Allocated)
                    {
                        AssignmentData = AssignmentData.IpPort(
                            detailsResult.Result.IPV4Address,
                            detailsResult.Result.Ports[0].Num)
                    };
                case GameServerState.Terminating:
                    return new PollResponse(PollStatus.Error) { Message = ServerIsTerminatingFriendlyError };
                default:
                    throw new InvalidEnumArgumentException(nameof(serverState), (int)serverState, typeof(GameServerState));
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occurred when polling the server status.");
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }
    }

    bool IsValid(PlayFabResult<GetEntityTokenResponse> result)
    {
        switch (result)
        {
            case null:
                logger.LogError(
                    "An error occurred when calling {method}. The result is null.", nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync));
                return false;
            case { Error: not null }:
                logger.LogError("An error occurred when calling {method}. The result is null. Error: {error}.", nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync), SerializeToJson(result.Error));
                return false;
            case { Result: { EntityToken.Length: > 0, Entity: { Id.Length: > 0, Type.Length: > 0 } } }:
                return true;
            default:
                logger.LogError("An error occurred when calling {method}. The result is null. Token is malformed. Token: {result}.", nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync), SerializeToJson(result.Result));
                return false;
        }
    }

    bool IsValid(PlayFabResult<RequestMultiplayerServerResponse> result)
    {
        switch (result)
        {
            case null:
                logger.LogError("An error occurred when calling {method}. The result is null.", nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync));
                return false;
            case { Error: not null }:
                logger.LogError("An error occurred when calling {method}. Error: {error}.", nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync), SerializeToJson(result.Error));
                return false;
            case { Result.SessionId.Length: > 0 }:
                return true;
            default:
                return false;
        }
    }

    bool IsValid(PlayFabResult<GetMultiplayerServerDetailsResponse> result)
    {
        switch (result)
        {
            case { Result: { State.Length: > 0, IPV4Address.Length: > 0, Ports.Count: > 0 } }:
            {
                return true;
            }
            default:
                logger.LogError("An error occurred when calling {method}. Details are malformed. Details: {result}.", nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync), SerializeToJson(result.Result));
                return false;
        }
    }

    static string SerializeToJson<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
}

/// <summary>
/// Factory for creating PlayFab API clients.
/// </summary>
public interface IPlayFabFactory
{
    /// <summary>
    /// Creates a PlayFab multiplayer instance API.
    /// </summary>
    IPlayFabMultiplayerInstanceAPI CreateMultiplayerInstanceApi(PlayFabApiSettings settings, PlayFabAuthenticationContext context);
}

/// <summary>
/// Implementation of <see cref="IPlayFabFactory"/>.
/// </summary>
public class PlayFabFactory : IPlayFabFactory
{
    public IPlayFabMultiplayerInstanceAPI CreateMultiplayerInstanceApi(PlayFabApiSettings settings, PlayFabAuthenticationContext context)
    {
        return new PlayFabMultiplayerInstanceAPI(settings, context);
    }
}

/// <summary>
/// Wrapper interface for PlayFab authentication API.
/// </summary>
public interface IPlayFabAuthenticationApi
{
    Task<PlayFabResult<GetEntityTokenResponse>> GetEntityTokenAsync(PlayFabApiSettings settings);
}

/// <summary>
/// Wrapper implementation for PlayFab authentication API.
/// </summary>
public class PlayFabAuthenticationApi : IPlayFabAuthenticationApi
{
    public Task<PlayFabResult<GetEntityTokenResponse>> GetEntityTokenAsync(PlayFabApiSettings settings)
    {
        return PlayFabAuthenticationAPI.GetEntityTokenAsync(new GetEntityTokenRequest(), settings);
    }
}
