using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace MultiplayAllocator;

public class MultiplayAllocator : MatchmakerAllocator
{
    private const string FleetId = "0115aeef-51f5-4260-83f7-cd304d0c635d";
    private const int BuildConfigId = 1136640;
    private const string DefaultRegion = "bd984d6f-37a6-473d-a766-8944ae439526";

    private ILogger<MultiplayAllocator> _logger;

    public MultiplayAllocator(ILogger<MultiplayAllocator> logger)
    {
        _logger = logger;
    }

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public override async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var createAllocationUrl = $"https://multiplay-stg.services.api.unity.com/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString();

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ServiceToken);

        var content = new StringContent(JsonSerializer.Serialize(new MultiplayAllocateRequest()
        {
            AllocationId = Guid.NewGuid().ToString(),
            BuildConfigurationId = BuildConfigId,
            RegionId = region ?? DefaultRegion,
            Payload = JsonSerializer.Serialize(request.MatchmakingResults)
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(createAllocationUrl, content);

        var responseContent = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return new AllocateResponse
            {
                Status = AllocateStatus.Error,
                AllocationData = new Dictionary<string, object>
                {
                    { "error", responseContent }
                }
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

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public override async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var allocationId = request.AllocationData["allocationId"].ToString();
        var getAllocationsUrl = $"https://multiplay.services.api.unity.com/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations/{allocationId}";

        var allocationTime = DateTimeOffset.FromUnixTimeMilliseconds((long)request.AllocationData["startTime"]);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ServiceToken);

        var allocation = await client.GetAsync(getAllocationsUrl);

        allocation.EnsureSuccessStatusCode();
        var responseContent = await allocation.Content.ReadAsStringAsync();
        var multiplayAllocation = JsonSerializer.Deserialize<MultiplayAllocationStatus>(responseContent);

        if (multiplayAllocation.Ready)
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

    [JsonPropertyName("ready")]
    public bool Ready { get; set; }

    [JsonPropertyName("ipv4")]
    public string Ipv4 { get; set; }

    [JsonPropertyName("gamePort")]
    public int GamePort { get; set; }

    [JsonPropertyName("serverId")]
    public string ServerId { get; set; }

    [JsonPropertyName("regionId")]
    public string RegionId { get; set; }
}
