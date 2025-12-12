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

namespace PlayfabAllocatorModule;

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

public class PlayfabAllocator : IMatchmakerAllocator
{
    const string DeveloperSecretKey = "DEVELOPER_SECRET_KEY";
    const string PlayfabBuildId = "PLAYFAB_BUILD_ID";
    const string PlayfabTitleId = "TITLE_ID";
    static readonly Dictionary<string, string> RegionMap = new() { ["us-east"] = "EastUs" };

    readonly IGameApiClient _gameApiClient;
    readonly Action<string, Exception?> LogDebug;
    readonly Action<string, Exception?> LogError;

    public PlayfabAllocator(IGameApiClient gameApiClient, ILogger<PlayfabAllocator> logger)
    {
        _gameApiClient = gameApiClient;
        LogError = (message, exception) =>
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(), "{ErrorMessage}")(logger, message, exception);
        LogDebug = (message, exception) =>
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(), "{DebugMessage}")(logger, message, exception);
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
            const string error = $"An error occured when retrieving secret for key '{DeveloperSecretKey}'.";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }

        try
        {
            PlayFabSettings.staticSettings.TitleId = (await _gameApiClient.SecretManager.GetSecret(context, PlayfabTitleId)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{PlayfabTitleId}'.";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }

        var playFabApiSettings = new PlayFabApiSettings { TitleId = PlayFabSettings.staticSettings.TitleId };

        string? buildId;
        try
        {
            buildId = (await _gameApiClient.SecretManager.GetSecret(context, PlayfabBuildId)).Value;
        }
        catch (Exception e)
        {
            const string error = $"An error occured when retrieving secret for key '{PlayfabBuildId}'.";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequest = new GetEntityTokenRequest();
            var entityTokenRequestResult =
                await PlayFabAuthenticationAPI.GetEntityTokenAsync(entityTokenRequest, playFabApiSettings);

            if (!IsValid(entityTokenRequestResult, out var errorMessage))
            {
                LogError(errorMessage, null);
                return new AllocateResponse(AllocateStatus.Error) { Message = errorMessage };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            const string error = "An error occured when retrieving the entity token.";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
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

            var preferredRegion = GetPreferredRegion(request);
            if (preferredRegion is null or "")
            {
                const string error =
                    "An error occured when retrieving the region in matchmaking properties. The region field must be present, non-null and non-empty.";
                LogError(error, null);
                return new AllocateResponse(AllocateStatus.Error) { Message = error };
            }

            var multiplayerServerRequest = new RequestMultiplayerServerRequest
            {
                BuildId = buildId,
                PreferredRegions = [preferredRegion],
                SessionId = request.MatchId
            };

            LogDebug($"Requesting an allocation for session id: {multiplayerServerRequest.SessionId}", null);

            var allocationResult = await multiplayerInstanceApi.RequestMultiplayerServerAsync(multiplayerServerRequest);

            if (IsValid(allocationResult, out var errorMessage))
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

            return new AllocateResponse(AllocateStatus.Error) { Message = errorMessage };
        }
        catch (Exception e)
        {
            const string error = "An error occured when allocating.";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }
    }

    [CloudCodeFunction(nameof(Poll))]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var developerSecretKey = await _gameApiClient.SecretManager.GetSecret(context, DeveloperSecretKey);
        var titleId = await _gameApiClient.SecretManager.GetSecret(context, SecretKey);

        PlayFabSettings.staticSettings.DeveloperSecretKey = developerSecretKey.Value;
        PlayFabSettings.staticSettings.TitleId = titleId.Value;
        var playFabApiSettings = new PlayFabApiSettings { TitleId = titleId.Value };

        GetEntityTokenResponse tokenRequestResponse;
        try
        {
            var entityTokenRequest = new GetEntityTokenRequest();
            var entityTokenRequestResult =
                await PlayFabAuthenticationAPI.GetEntityTokenAsync(entityTokenRequest, playFabApiSettings);

            if (!IsValid(entityTokenRequestResult, out var errorMessage))
            {
                LogError(errorMessage, null);
                return new PollResponse(PollStatus.Error) { Message = errorMessage };
            }

            tokenRequestResponse = entityTokenRequestResult.Result;
        }
        catch (Exception e)
        {
            var error = $"An error occured when retrieving the entity token. Error: {e.Message}";
            LogError(error, e);
            return new PollResponse(PollStatus.Error) { Message = error };
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

            LogDebug($"Requesting details for session id: {multiplayerServerDetailsRequest.SessionId}", null);

            var detailsResult =
                await multiplayerInstanceApi.GetMultiplayerServerDetailsAsync(multiplayerServerDetailsRequest);

            if (IsValid(detailsResult, out var errorMessage))
            {
                switch (detailsResult.Result.State)
                {
                    case "StandingBy":
                        return new PollResponse(PollStatus.Pending);
                    case "Allocated":
                        return new PollResponse(PollStatus.Allocated)
                        {
                            AssignmentData = AssignmentData.IpPort(
                                detailsResult.Result.IPV4Address,
                                detailsResult.Result.Ports[0].Num)
                        };
                    default:
                        var error =
                            $"An error occured when polling the server status. Server state: {detailsResult.Result.State}";
                        LogError(error, null);
                        return new PollResponse(PollStatus.Error) { Message = error };
                }
            }

            LogError(errorMessage, null);
            return new PollResponse(PollStatus.Error) { Message = errorMessage };
        }
        catch (Exception e)
        {
            var error = $"An error occured when polling the server status. Error: {e.Message}";
            LogError(error, e);
            return new PollResponse(PollStatus.Error) { Message = error };
        }
    }

    static string? GetPreferredRegion(AllocateRequest request)
    {
        if (!request.MatchmakingResults.MatchProperties.TryGetValue("Region", out var regionValue))
        {
            return null;
        }

        var unityRegion = regionValue.ToString() ?? string.Empty;
        return RegionMap.GetValueOrDefault(unityRegion);
    }

    bool IsValid(PlayFabResult<GetEntityTokenResponse> entityTokenRequestResult, out string errorMessage)
    {
        switch (entityTokenRequestResult)
        {
            case null:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null.";
                LogError(errorMessage, null);
                return false;
            case { Error: not null }:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}.";
                LogError($"{errorMessage} Error: {SerializeToJson(entityTokenRequestResult.Error)}.", null);
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
                errorMessage = string.Empty;
                return true;
            default:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}.";
                LogError($"{errorMessage} Token is malformed. Token: {SerializeToJson(entityTokenRequestResult.Result)}.", null);
                return false;
        }
    }

    static bool IsValid(PlayFabResult<RequestMultiplayerServerResponse> requestMultiplayerServerResult,
        out string errorMessage)
    {
        switch (requestMultiplayerServerResult)
        {
            case null:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. The result is null.";
                return false;
            case { Error: not null }:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. Error: {SerializeToJson(requestMultiplayerServerResult.Error)}.";
                return false;
            default:
                errorMessage = string.Empty;
                return true;
        }
    }

    static bool IsValid(PlayFabResult<GetMultiplayerServerDetailsResponse> getMultiplayerServerDetailsResult,
        out string errorMessage)
    {
        switch (getMultiplayerServerDetailsResult)
        {
            case null:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. The result is null.";
                return false;
            case { Error: not null }:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. Error: {SerializeToJson(getMultiplayerServerDetailsResult.Error)}.";
                return false;
            case { Result: { State.Length: > 0, IPV4Address.Length: > 0, Ports.Count: > 0 } }:
            {
                errorMessage = string.Empty;
                return true;
            }
            default:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. Details are malformed. Details: {SerializeToJson(getMultiplayerServerDetailsResult.Result)}.";
                return false;
        }
    }

    static string SerializeToJson<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
}
