using System;
using System.Collections.Generic;
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
    }
}

public class PlayFabAllocator : IMatchmakerAllocator
{
    const string AllocationUserFriendlyError = "An error occured when allocating.";
    const string PollUserFriendlyError = "An error occured when polling the server status.";
    const string DefaultPlayFabRegion = "EastUs";

    /// <summary>
    /// You will need to set up a secret in the <a
    /// href="https://cloud.unity.com">Unity Dashboard</a>
    /// with the <c>DEVELOPER_SECRET_KEY</c> key
    /// containing your PlayFab Developer Secret Key.
    /// </summary>
    const string DeveloperSecretKey = "DEVELOPER_SECRET_KEY";

    /// <summary>
    /// You will need to set up a secret in the <a
    /// href="https://cloud.unity.com">Unity Dashboard</a> with the
    /// <c>PLAYFAB_BUILD_ID</c> key containing your PlayFab Build Id.
    /// </summary>
    const string PlayFabBuildId = "PLAYFAB_BUILD_ID";

    /// <summary>
    /// You will need to set up a secret in the <a
    /// href="https://cloud.unity.com">Unity Dashboard</a> with
    /// the <c>TITLE_ID</c> key containing your PlayFab Title Id.
    /// </summary>
    const string PlayFabTitleId = "TITLE_ID";

    readonly ILogger _logger;
    readonly IGameApiClient _gameApiClient;

    public PlayFabAllocator(IGameApiClient gameApiClient, ILogger<PlayFabAllocator> logger)
    {
        _gameApiClient = gameApiClient;
        _logger = logger;
    }

    [CloudCodeFunction(nameof(Allocate))]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        try
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = (await _gameApiClient.SecretManager.GetSecret(context, DeveloperSecretKey)).Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"An error occured when retrieving secrets for key '{DeveloperSecretKey}'.");
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }

        try
        {
            PlayFabSettings.staticSettings.TitleId = (await _gameApiClient.SecretManager.GetSecret(context, PlayFabTitleId)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{PlayFabTitleId}'.";
            _logger.LogError(e, error);
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }

        var playFabApiSettings = new PlayFabApiSettings { TitleId = PlayFabSettings.staticSettings.TitleId };

        string? buildId;
        try
        {
            buildId = (await _gameApiClient.SecretManager.GetSecret(context, PlayFabBuildId)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{PlayFabBuildId}'.";
            _logger.LogError(e, error);
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequest = new GetEntityTokenRequest();
            var entityTokenRequestResult =
                await PlayFabAuthenticationAPI.GetEntityTokenAsync(entityTokenRequest, playFabApiSettings);

            if (!IsValid(entityTokenRequestResult))
            {
                return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            const string error = "An error occured when retrieving the entity token.";
            _logger.LogError(e, error);
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

            var multiplayerInstanceApi = new PlayFabMultiplayerInstanceAPI(playFabApiSettings, authenticationContext);

            var preferredRegion = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultPlayFabRegion;
            if (preferredRegion is null or "")
            {
                const string error =
                    "An error occured when retrieving the region in matchmaking properties. The region field must be present, non-null and non-empty.";
                _logger.LogError(error);
                return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
            }

            var multiplayerServerRequest = new RequestMultiplayerServerRequest
            {
                BuildId = buildId,
                PreferredRegions = [preferredRegion],
                SessionId = request.MatchId
            };

            _logger.LogDebug($"Requesting an allocation for session id: {multiplayerServerRequest.SessionId}");

            var allocationResult = await multiplayerInstanceApi.RequestMultiplayerServerAsync(multiplayerServerRequest);

            if (IsValid(allocationResult))
            {
                return new AllocateResponse(AllocateStatus.Created)
                {
                    AllocationData = new Dictionary<string, object>
                    {
                        { "sessionId", allocationResult.Result.SessionId },
                        { "playfabRegion", allocationResult.Result.Region },
                        { "startTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                        { "matchId", allocationResult.Result.SessionId }
                    }
                };
            }

            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }
        catch (Exception e)
        {
            const string error = "An error occured when allocating.";
            _logger.LogError(e, error);
            return new AllocateResponse(AllocateStatus.Error) { Message = AllocationUserFriendlyError };
        }
    }

    [CloudCodeFunction(nameof(Poll))]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        try
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = (await _gameApiClient.SecretManager.GetSecret(context, DeveloperSecretKey)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{DeveloperSecretKey}'.";
            _logger.LogError(e, error);
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }

        try
        {
            PlayFabSettings.staticSettings.TitleId = (await _gameApiClient.SecretManager.GetSecret(context, PlayFabTitleId)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{PlayFabTitleId}'.";
            _logger.LogError(e, error);
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }

        var playFabApiSettings = new PlayFabApiSettings { TitleId = PlayFabSettings.staticSettings.TitleId };

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequest = new GetEntityTokenRequest();
            var entityTokenRequestResult =
                await PlayFabAuthenticationAPI.GetEntityTokenAsync(entityTokenRequest, playFabApiSettings);

            if (!IsValid(entityTokenRequestResult))
            {
                return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            const string error = "An error occured when retrieving the entity token.";
            _logger.LogError(e, error);
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

            var multiplayerInstanceApi = new PlayFabMultiplayerInstanceAPI(playFabApiSettings, authenticationContext);

            var multiplayerServerDetailsRequest = new GetMultiplayerServerDetailsRequest
            {
                SessionId = request.AllocationData["sessionId"].ToString()
            };

            _logger.LogDebug($"Requesting details for session id: {multiplayerServerDetailsRequest.SessionId}");

            var detailsResult =
                await multiplayerInstanceApi.GetMultiplayerServerDetailsAsync(multiplayerServerDetailsRequest);

            if (!IsValid(detailsResult))
            {
                return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }

            switch (detailsResult.Result.State)
            {
                case "StandingBy":
                case "Initializing":
                    return new PollResponse(PollStatus.Pending);
                case "Active":
                    return new PollResponse(PollStatus.Allocated)
                    {
                        AssignmentData = AssignmentData.IpPort(
                            detailsResult.Result.IPV4Address,
                            detailsResult.Result.Ports[0].Num)
                    };
                default:
                    _logger.LogError($"An error occured when polling the server status. Server state: {detailsResult.Result.State}");
                    return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occured when polling the server status.");
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }
    }

    bool IsValid(PlayFabResult<GetEntityTokenResponse> entityTokenRequestResult)
    {
        switch (entityTokenRequestResult)
        {
            case null:
                _logger.LogError(
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                _logger.LogError($"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null. Error: {SerializeToJson(entityTokenRequestResult.Error)}.");
                return false;
            case
            {
                Result:
                {
                    EntityToken: not null and not "",
                    Entity:
                    {
                        Id: not null and not "",
                        Type: not null and not ""
                    }
                }
            }:
                return true;
            default:
                _logger.LogError($"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null. Token is malformed. Token: {SerializeToJson(entityTokenRequestResult.Result)}.");
                return false;
        }
    }

    bool IsValid(PlayFabResult<RequestMultiplayerServerResponse> requestMultiplayerServerResult)
    {
        switch (requestMultiplayerServerResult)
        {
            case null:
                _logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                _logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. Error: {SerializeToJson(requestMultiplayerServerResult.Error)}.");
                return false;
            default:
                return true;
        }
    }

    bool IsValid(PlayFabResult<GetMultiplayerServerDetailsResponse> getMultiplayerServerDetailsResult)
    {
        switch (getMultiplayerServerDetailsResult)
        {
            case null:
                _logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                _logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. Error: {SerializeToJson(getMultiplayerServerDetailsResult.Error)}.");
                return false;
            case { Result: { State.Length: > 0, IPV4Address.Length: > 0, Ports.Count: > 0 } }:
            {
                return true;
            }
            default:
                _logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. Details are malformed. Details: {SerializeToJson(getMultiplayerServerDetailsResult.Result)}.");
                return false;
        }
    }

    static string SerializeToJson<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
}
