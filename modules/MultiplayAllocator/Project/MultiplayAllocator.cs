using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;

namespace HelloWorld;

public class MultiplayAllocator : MatchmakerAllocator
{
    private const string FleetId = "your_fleet_id_here";
    private const string BuildConfigId = "your_build_config_id_here";
    private const string DefaultRegion = "your_default_region_here";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public override async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var createAllocationUrl = $"https://multiplay.services.api.unity.com/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.TryGetValue("region", out var regionValue) ? regionValue.ToString() : DefaultRegion;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", context.ServiceToken);

        var content = new StringContent(JsonSerializer.Serialize(new
        {
            allocationId = FleetId,
            buildConfigurationId = BuildConfigId,
            regionId = region,
            payload = request.MatchmakingResults
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(createAllocationUrl, content);

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
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

class MultiplayAllocateResponse
{
    public string AllocationId { get; set; }
}

class MultiplayAllocationStatus
{
    public string AllocationId { get; set; }

    public bool Ready { get; set; }

    public string Ipv4 { get; set; }

    public int GamePort { get; set; }

    public string ServerId { get; set; }

    public string RegionId { get; set; }
}
