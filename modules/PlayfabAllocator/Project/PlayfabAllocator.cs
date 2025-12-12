using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        ChangeThis();

        var playFabApiSettings = new PlayFabApiSettings { TitleId = "D3C5F" };

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
            var error = $"An error occured when retrieving the entity token. Error: {e.Message}";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }

        try
        {
            var authenticationContext = new PlayFabAuthenticationContext
            {
                EntityId    = tokenRequestResponse.Entity.Id,
                EntityToken = tokenRequestResponse.EntityToken,
                EntityType  = tokenRequestResponse.Entity.Type
            };

            var multiplayerInstanceApi = new PlayFabMultiplayerInstanceAPI(playFabApiSettings, authenticationContext);

            var preferredRegion = GetPreferredRegion(request);
            if (preferredRegion is null or "")
            {
                const string error = "An error occured when retrieving the region in matchmaking properties. The region field must be present, non-null and non-empty.";
                LogError(error, null);
                return new AllocateResponse(AllocateStatus.Error) { Message = error };
            }

            var multiplayerServerRequest = new RequestMultiplayerServerRequest
            {
                BuildId          = ChangeThat(),
                PreferredRegions = [preferredRegion],
                SessionId        = request.MatchId
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

            LogError(errorMessage, null);
            return new AllocateResponse(AllocateStatus.Error) { Message = errorMessage };
        }
        catch (Exception e)
        {
            var error = $"An error occured when allocating. Error: {e.Message}";
            LogError(error, e);
            return new AllocateResponse(AllocateStatus.Error) { Message = error };
        }
    }

    [CloudCodeFunction(nameof(Poll))]
    public Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        throw new NotImplementedException();
    }

    static void ChangeThis()
    {
        PlayFabSettings.staticSettings.DeveloperSecretKey = "G5KX8GQWP5II5XYWIHOEI753ZGPXSEFJR5HBS7AKA5ABPIAUW8";
        PlayFabSettings.staticSettings.TitleId = "D3C5F";
    }

    static string ChangeThat()
    {
        return "b874afc8-358b-4b87-89c7-25a0e10742bf";
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

    static bool IsValid(PlayFabResult<GetEntityTokenResponse> entityTokenRequestResult, out string errorMessage)
    {
        switch (entityTokenRequestResult)
        {
            case null:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null.";
                return false;
            case { Error: not null }:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. Error: {SerializeToJson(entityTokenRequestResult.Error)}.";
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
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. Token is malformed. Token: {SerializeToJson(entityTokenRequestResult.Result)}.";
                return false;
        }
    }

    static bool IsValid(PlayFabResult<RequestMultiplayerServerResponse> entityTokenRequestResult, out string errorMessage)
    {
        switch (entityTokenRequestResult)
        {
            case null:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. The result is null.";
                return false;
            case { Error: not null }:
                errorMessage =
                    $"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. Error: {SerializeToJson(entityTokenRequestResult.Error)}.";
                return false;
            default:
                errorMessage = string.Empty;
                return true;
        }
    }

    static string SerializeToJson<T>(T obj)
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented);
    }
}
