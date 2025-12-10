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
    }
}

public class GameLiftAllocator : MatchmakerAllocator
{
    private readonly IGameApiClient _gameApiClient;

    // Configuration - users should modify these constants for their setup
    private const string GameSessionQueueName = "MyQueue"; // TODO: Replace with actual queue name
    private const int DefaultMaximumPlayerSessionCount = 10;
    private const string DefaultAwsRegion = "eu-west-2";

    // Secret names - these must match the secrets stored in Unity Dashboard
    private const string AwsAccessKeyIdSecretName = "AWS_ACCESS_KEY_ID";
    private const string AwsSecretAccessKeySecretName = "AWS_SECRET_ACCESS_KEY";

    // Map the Unity regions to your AWS fleet regions. // TODO: Update this with Unity QoS actual regions
    private static readonly Dictionary<string, string> RegionMap = new()
    {
        ["us-east"] = "us-east-1",
        ["us-west"] = "us-west-2",
        ["eu-west"] = "eu-west-1",
        ["eu-central"] = "eu-central-1",
        ["asia-east"] = "ap-northeast-1",
        ["asia-southeast"] = "ap-southeast-1"
    };

    public GameLiftAllocator(IGameApiClient gameApiClient)
    {
        _gameApiClient = gameApiClient;
    }

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public override async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        // Determine AWS region from match properties or use default
        var awsRegion = DefaultAwsRegion;
        if (request.MatchmakingResults.MatchProperties.TryGetValue("region", out var regionValue))
        {
            var unityRegion = regionValue?.ToString() ?? "";
            if (RegionMap.TryGetValue(unityRegion, out var mappedRegion))
            {
                awsRegion = mappedRegion;
            }
        }

        try
        {
            // Retrieve AWS credentials from Unity Secret Manager
            var accessKeyId = await _gameApiClient.SecretManager.GetSecret(context, AwsAccessKeyIdSecretName);
            var secretAccessKey = await _gameApiClient.SecretManager.GetSecret(context, AwsSecretAccessKeySecretName);

            // Create GameLift client with credentials from secrets
            var credentials = new BasicAWSCredentials(accessKeyId.Value, secretAccessKey.Value);
            var config = new AmazonGameLiftConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
            };
            using var client = new AmazonGameLiftClient(credentials, config);

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

            return new AllocateResponse
            {
                Status = AllocateStatus.Created,
                AllocationData = new Dictionary<string, object>
                {
                    { "placementId", placement.PlacementId },
                    { "awsRegion", awsRegion },
                    { "startTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { "matchId", request.MatchId }
                }
            };
        }
        catch (Exception ex)
        {
            return new AllocateResponse
            {
                Status = AllocateStatus.Error,
                Message = $"Failed to start game session placement: {ex.Message}"
            };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public override async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var placementId = request.AllocationData["placementId"]?.ToString();
        var awsRegion = request.AllocationData["awsRegion"]?.ToString() ?? DefaultAwsRegion;

        if (string.IsNullOrEmpty(placementId))
        {
            return new PollResponse
            {
                Status = PollStatus.Error,
                Message = "Missing placementId in allocation data"
            };
        }

        try
        {
            // Retrieve AWS credentials from Unity Secret Manager
            var accessKeyId = await _gameApiClient.SecretManager.GetSecret(context, AwsAccessKeyIdSecretName);
            var secretAccessKey = await _gameApiClient.SecretManager.GetSecret(context, AwsSecretAccessKeySecretName);

            // Create GameLift client with credentials from secrets
            var credentials = new BasicAWSCredentials(accessKeyId.Value, secretAccessKey.Value);
            var config = new AmazonGameLiftConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
            };
            using var client = new AmazonGameLiftClient(credentials, config);

            var describeRequest = new DescribeGameSessionPlacementRequest
            {
                PlacementId = placementId
            };

            var response = await client.DescribeGameSessionPlacementAsync(describeRequest);
            var placement = response.GameSessionPlacement;

            return placement.Status.Value switch
            {
                "PENDING" => new PollResponse
                {
                    Status = PollStatus.Pending
                },
                "FULFILLED" => new PollResponse
                {
                    Status = PollStatus.Allocated,
                    AssignmentData = new IpPortAssignmentData
                    {
                        Ip = placement.IpAddress,
                        Port = placement.Port
                    }
                },
                "TIMED_OUT" => new PollResponse
                {
                    Status = PollStatus.Error,
                    Message = "Game session placement timed out"
                },
                "CANCELLED" => new PollResponse
                {
                    Status = PollStatus.Error,
                    Message = "Game session placement was cancelled"
                },
                "FAILED" => new PollResponse
                {
                    Status = PollStatus.Error,
                    Message = "Game session placement failed"
                },
                _ => new PollResponse
                {
                    Status = PollStatus.Error,
                    Message = $"Unknown placement status: {placement.Status.Value}"
                }
            };
        }
        catch (Exception ex)
        {
            return new PollResponse
            {
                Status = PollStatus.Error,
                Message = $"Failed to describe game session placement: {ex.Message}"
            };
        }
    }
}
