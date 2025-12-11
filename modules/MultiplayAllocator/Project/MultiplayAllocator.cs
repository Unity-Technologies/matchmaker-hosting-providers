using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MultiplayAllocator;

public class MultiplayAllocator(ILogger<MultiplayAllocator> logger) : MatchmakerAllocator
{
    // Configuration - users should modify these constants for their setup
    private const string FleetId = "your_fleet_id";
    private const int BuildConfigId = 0;
    private const string DefaultRegion = "your_default_region";

    // Service constants
    private const string MultiplayHost = "multiplay.services.api.unity.com";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public override async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var createAllocationUrl = $"https://{MultiplayHost}/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultRegion;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ServiceToken);

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(new MultiplayAllocateRequest()
            {
                AllocationId = Guid.NewGuid().ToString(),
                BuildConfigurationId = BuildConfigId,
                RegionId = region,
                Payload = JsonSerializer.Serialize(request.MatchmakingResults)
            }), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(createAllocationUrl, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error allocating Multiplay {error}", responseContent);

                return new AllocateResponse
                {
                    Status = AllocateStatus.Error,
                    Message = responseContent
                };
            }

            var multiplayAllocation = JsonSerializer.Deserialize<MultiplayAllocateResponse>(responseContent);

            return new AllocateResponse
            {
                Status = AllocateStatus.Created,
                AllocationData = new Dictionary<string, object>
                {
                    { "allocationId", multiplayAllocation.AllocationId },
                    { "startTime", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                    { "matchId", request.MatchId },
                    { "region", region }
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error allocating Multiplay");

            return new AllocateResponse
            {
                Status = AllocateStatus.Error,
                Message = ex.Message
            };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public override async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var allocationId = request.AllocationData["allocationId"].ToString();
        var getAllocationsUrl = $"https://{MultiplayHost}/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations/{allocationId}";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ServiceToken);

        try
        {
            var allocation = await client.GetAsync(getAllocationsUrl);

            var responseContent = await allocation.Content.ReadAsStringAsync();
            if (!allocation.IsSuccessStatusCode)
            {
                return new PollResponse
                {
                    Status = PollStatus.Error,
                    Message = responseContent
                };
            }

            var multiplayAllocation = JsonSerializer.Deserialize<MultiplayAllocationStatus>(responseContent);

            if (!string.IsNullOrEmpty(multiplayAllocation.Fulfilled))
            {
                if (!multiplayAllocation.Readiness || !string.IsNullOrEmpty(multiplayAllocation.Ready))
                {
                    if (!string.IsNullOrEmpty(multiplayAllocation.Ipv4) && multiplayAllocation.GamePort != 0)
                    {
                        return new PollResponse
                        {
                            Status = PollStatus.Allocated,
                            AssignmentData = new IpPortAssignmentData
                            {
                                Ip = multiplayAllocation.Ipv4,
                                Port = multiplayAllocation.GamePort
                            },
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling Multiplay");

            return new PollResponse
            {
                Status = PollStatus.Error,
                Message = ex.Message
            };
        }

        return new PollResponse
        {
            Status = PollStatus.Pending,
        };
    }
}

class MultiplayAllocateRequest
{
    [JsonPropertyName("allocationId")]
    public string AllocationId { get; set; }
    [JsonPropertyName("buildConfigurationId")]
    public int BuildConfigurationId { get; set; }
    [JsonPropertyName("regionId")]
    public string RegionId { get; set; }
    [JsonPropertyName("payload")]
    public string Payload { get; set; }
}

class MultiplayAllocateResponse
{
    [JsonPropertyName("allocationId")]
    public string AllocationId { get; set; }
}

class MultiplayAllocationStatus
{
    [JsonPropertyName("allocationId")]
    public string AllocationId { get; set; }

    [JsonPropertyName("fulfilled")]
    public string Fulfilled { get; set; }

    [JsonPropertyName("readiness")]
    public bool Readiness { get; set; }

    [JsonPropertyName("ready")]
    public string Ready { get; set; }

    [JsonPropertyName("ipv4")]
    public string Ipv4 { get; set; }

    [JsonPropertyName("gamePort")]
    public int GamePort { get; set; }
}
