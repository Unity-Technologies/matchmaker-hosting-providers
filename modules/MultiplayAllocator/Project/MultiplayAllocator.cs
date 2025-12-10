using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;

namespace HelloWorld;

public class MultiplayAllocator : MatchmakerAllocator
{
    private const string EnvironmentId = "your_environment_id_here";
    private const string ProjectId = "your_project_id_here";
    private const string FleetId = "your_fleet_id_here";
    private const string BuildConfigId = "your_build_config_id_here";
    private const string DefaultRegion = "your_default_region_here";

    private const string ServiceAccountToken = "your_service_account_token_here";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public override async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var createAllocationUrl = $"https://multiplay.services.api.unity.com/v1/allocations/projects/{ProjectId}/environments/{EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.TryGetValue("region", out var regionValue) ? regionValue.ToString() : DefaultRegion;

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ServiceAccountToken);

        var content = new StringContent(JsonConvert.SerializeObject(new
        {
            allocationId = FleetId,
            buildConfigurationId = BuildConfigId,
            regionId = region,
            payload = request.MatchmakingResults
        }), Encoding.UTF8, "application/json");

        var response = await client.PostAsync(createAllocationUrl, content);

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();
        var multiplayAllocation = JsonConvert.DeserializeObject<MultiplayAllocateResponse>(responseContent);

        return new AllocateResponse
        {
            Status = "created",
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
        var getAllocationsUrl = $"https://multiplay.services.api.unity.com/v1/allocations/projects/{ProjectId}/environments/{EnvironmentId}/fleets/{FleetId}/allocations/{allocationId}";

        var allocationTime = DateTimeOffset.FromUnixTimeMilliseconds((long)request.AllocationData["startTime"]);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ServiceAccountToken);

        var allocation = await client.GetAsync(getAllocationsUrl);

        allocation.EnsureSuccessStatusCode();
        var responseContent = await allocation.Content.ReadAsStringAsync();
        var multiplayAllocation = JsonConvert.DeserializeObject<MultiplayAllocationStatus>(responseContent);

        if (multiplayAllocation.Ready)
        {
            return new PollResponse
            {
                Status = "allocated",
                AssignmentType = "MultiplayAssignment",
                Ip = multiplayAllocation.Ipv4,
                Port = multiplayAllocation.GamePort,
                CustomData = new Dictionary<string, object>
                {
                    { "serverId", multiplayAllocation.ServerId },
                    { "regionId", multiplayAllocation.RegionId }
                },
                AllocationCreatedTime = allocationTime
            };
        }

        return new PollResponse
        {
            Status = "pending",
            AllocationCreatedTime = allocationTime
        };
    }
}

class MultiplayAllocateResponse
{
    [JsonProperty("allocationId")]
    public string AllocationId { get; set; }
}

class MultiplayAllocationStatus
{
    [JsonProperty("allocationId")]
    public string AllocationId { get; set; }

    [JsonProperty("ready")]
    public bool Ready { get; set; }

    [JsonProperty("ipv4")]
    public string Ipv4 { get; set; }

    [JsonProperty("gamePort")]
    public int GamePort { get; set; }

    [JsonProperty("serverId")]
    public string ServerId { get; set; }

    [JsonProperty("regionId")]
    public string RegionId { get; set; }
}
