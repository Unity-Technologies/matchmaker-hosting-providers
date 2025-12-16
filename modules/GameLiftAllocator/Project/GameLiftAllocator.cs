using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.GameLift;
using Amazon.GameLift.Model;
using Amazon.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using IExecutionContext = Unity.Services.CloudCode.Core.IExecutionContext;
using Microsoft.Extensions.Logging;

namespace GameLiftAllocatorModule;

/// <summary>
/// Module configuration for dependency injection.
/// Registers IGameApiClient as a singleton for accessing Unity services like Secret Manager.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddScoped<IGameLiftFactory, GameLiftFactory>();
    }
}

public class GameLiftAllocator(IGameApiClient gameApiClient, IGameLiftFactory gameLiftFactory, ILogger<GameLiftAllocator> logger) : IMatchmakerAllocator
{
    // Configuration - users should modify these constants for their setup
    private const string GameSessionQueueName = "MyQueue"; // TODO: Replace with actual queue name
    private const int DefaultMaximumPlayerSessionCount = 10;
    private const string DefaultAwsRegion = "eu-west-2";

    // Secret names - these must match the secrets stored in Unity Dashboard
    private const string AwsAccessKeyIdSecretName = "AWS_ACCESS_KEY_ID";
    private const string AwsSecretAccessKeySecretName = "AWS_SECRET_ACCESS_KEY";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        // Determine AWS region from match properties or use default
        var region = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultAwsRegion;

        try
        {
            // Retrieve AWS credentials from Unity Secret Manager
            var accessKeyId = await gameApiClient.SecretManager.GetSecret(context, AwsAccessKeyIdSecretName);
            var secretAccessKey = await gameApiClient.SecretManager.GetSecret(context, AwsSecretAccessKeySecretName);

            // Create GameLift client with credentials from secrets
            using var client = gameLiftFactory.Create(accessKeyId.Value, secretAccessKey.Value, region);

            // Serialize match data for the game server
            var gameSessionData = JsonSerializer.Serialize(request.MatchmakingResults);

            // Start game session placement
            var placementRequest = new StartGameSessionPlacementRequest
            {
                PlacementId = request.MatchId, // Use matchId for idempotency
                GameSessionQueueName = GameSessionQueueName,
                MaximumPlayerSessionCount = DefaultMaximumPlayerSessionCount,
                GameSessionData = gameSessionData
            };

            var response = await client.StartGameSessionPlacementAsync(placementRequest);
            var placement = response.GameSessionPlacement;

            return new AllocateResponse(AllocateStatus.Created)
            {
                AllocationData = new Dictionary<string, object>
                {
                    { "placementId", placement.PlacementId },
                    { "awsRegion", region },
                    { "startTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { "matchId", request.MatchId }
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error starting game session placement");

            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"Failed to start game session placement: {ex}"
            };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var placementId = request.AllocationData["placementId"].ToString();
        var region = request.AllocationData["awsRegion"].ToString();

        if (region == null)
        {
            return new PollResponse(PollStatus.Error)
            {
                Message = "Poll region was not specified"
            };
        }

        try
        {
            // Retrieve AWS credentials from Unity Secret Manager
            var accessKeyId = await gameApiClient.SecretManager.GetSecret(context, AwsAccessKeyIdSecretName);
            var secretAccessKey = await gameApiClient.SecretManager.GetSecret(context, AwsSecretAccessKeySecretName);

            // Create GameLift client with credentials from secrets
            using var client = gameLiftFactory.Create(accessKeyId.Value, secretAccessKey.Value, region);

            var describeRequest = new DescribeGameSessionPlacementRequest
            {
                PlacementId = placementId
            };

            var response = await client.DescribeGameSessionPlacementAsync(describeRequest);
            var placement = response.GameSessionPlacement;

            return placement.Status.Value switch
            {
                "PENDING" => new PollResponse(PollStatus.Pending),
                "FULFILLED" => new PollResponse(PollStatus.Allocated)
                {
                    AssignmentData = AssignmentData.IpPort(placement.IpAddress, placement.Port),
                },
                "TIMED_OUT" => new PollResponse(PollStatus.Error)
                {
                    Message = "Game session placement timed out"
                },
                "CANCELLED" => new PollResponse(PollStatus.Error)
                {
                    Message = "Game session placement was cancelled"
                },
                "FAILED" => new PollResponse(PollStatus.Error)
                {
                    Message = "Game session placement failed"
                },
                _ => new PollResponse(PollStatus.Error)
                {
                    Message = $"Unknown placement status: {placement.Status.Value}"
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to describe game session placement");

            return new PollResponse(PollStatus.Error)
            {
                Message = $"Failed to describe game session placement: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Factory for creating Amazon GameLift clients.
/// </summary>
public interface IGameLiftFactory
{
    /// <summary>
    /// Creates an Amazon GameLift client with the specified credentials and region.
    /// </summary>
    /// <param name="accessKeyId">The access key ID for the AWS account.</param>
    /// <param name="secretAccessKey">The secret access key for the AWS account.</param>
    /// <param name="region">The AWS region to use.</param>
    /// <returns>An instance of <see cref="IAmazonGameLift"/>.</returns>
    IAmazonGameLift Create(string accessKeyId, string secretAccessKey, string region);
}

/// <summary>
/// Implementation of <see cref="IGameLiftFactory"/> that creates Amazon GameLift clients.
/// </summary>
public class GameLiftFactory : IGameLiftFactory
{
    /// <summary>
    /// Creates an Amazon GameLift client with the specified credentials and region.
    /// </summary>
    /// <param name="accessKeyId">The access key ID for the AWS account.</param>
    /// <param name="secretAccessKey">The secret access key for the AWS account.</param>
    /// <param name="region">The AWS region to use.</param>
    /// <returns>An instance of <see cref="IAmazonGameLift"/>.</returns>
    public IAmazonGameLift Create(string accessKeyId, string secretAccessKey, string region)
    {
        var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
        var config = new AmazonGameLiftConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region)
        };
        return new AmazonGameLiftClient(credentials, config);
    }
}
