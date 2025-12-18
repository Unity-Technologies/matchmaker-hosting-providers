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
        config.Dependencies.AddScoped<IPlayFabFactory, PlayFabFactory>();
        config.Dependencies.AddScoped<IPlayFabAuthenticationApi, PlayFabAuthenticationApi>();
    }
}

public class PlayFabAllocator(IGameApiClient gameApiClient, IPlayFabFactory playFabFactory, IPlayFabAuthenticationApi authenticationApi, ILogger<PlayFabAllocator> logger)
    : IMatchmakerAllocator
{
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
    const string AllocationUserFriendlyError = "An error occured when allocating.";
    const string PollUserFriendlyError = "An error occured when polling the server status.";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        try
        {
            PlayFabSettings.staticSettings.DeveloperSecretKey = (await gameApiClient.SecretManager.GetSecret(context, PlayFabSecretKeySecretName)).Value;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"An error occured when retrieving secret for key '{PlayFabSecretKeySecretName}'.");
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
            const string error = "An error occured when retrieving the entity token.";
            logger.LogError(e, error);
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
            if (preferredRegion is "")
            {
                const string error =
                    "An error occured when retrieving the region in matchmaking properties. The region field must be non-empty.";
                logger.LogError(error);
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
            const string error = "An error occured when allocating.";
            logger.LogError(e, error);
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
            const string error = $"An error occured when retrieving secret for key '{PlayFabSecretKeySecretName}'.";
            logger.LogError(e, error);
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
            const string error = "An error occured when retrieving the entity token.";
            logger.LogError(e, error);
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
                    logger.LogError("An error occured when polling the server status. Server state: {state}", detailsResult.Result.State);
                    return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "An error occured when polling the server status.");
            return new PollResponse(PollStatus.Error) { Message = PollUserFriendlyError };
        }
    }

    bool IsValid(PlayFabResult<GetEntityTokenResponse> entityTokenRequestResult)
    {
        switch (entityTokenRequestResult)
        {
            case null:
                logger.LogError(
                    $"An error occured when calling {nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                logger.LogError("An error occured when calling {method}. The result is null. Error: {error}.", nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync), SerializeToJson(entityTokenRequestResult.Error));
                return false;
            case { Result: { EntityToken.Length: > 0, Entity: { Id.Length: > 0, Type.Length: > 0 } } }:
                return true;
            default:
                logger.LogError("An error occured when calling {method}. The result is null. Token is malformed. Token: {result}.", nameof(PlayFabAuthenticationAPI.GetEntityTokenAsync), SerializeToJson(entityTokenRequestResult.Result));
                return false;
        }
    }

    bool IsValid(PlayFabResult<RequestMultiplayerServerResponse> requestMultiplayerServerResult)
    {
        switch (requestMultiplayerServerResult)
        {
            case null:
                logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                logger.LogError("An error occured when calling {method}. Error: {error}.", nameof(PlayFabMultiplayerInstanceAPI.RequestMultiplayerServerAsync), SerializeToJson(requestMultiplayerServerResult.Error));
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
                logger.LogError($"An error occured when calling {nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync)}. The result is null.");
                return false;
            case { Error: not null }:
                logger.LogError("An error occured when calling {method}. Error: {error}.", nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync), SerializeToJson(getMultiplayerServerDetailsResult.Error));
                return false;
            case { Result: { State.Length: > 0, IPV4Address.Length: > 0, Ports.Count: > 0 } }:
            {
                return true;
            }
            default:
                logger.LogError("An error occured when calling {method}. Details are malformed. Details: {result}.", nameof(PlayFabMultiplayerInstanceAPI.GetMultiplayerServerDetailsAsync), SerializeToJson(getMultiplayerServerDetailsResult.Result));
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
