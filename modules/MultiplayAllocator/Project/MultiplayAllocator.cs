using System.Collections.Generic;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace MultiplayAllocatorModule;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddScoped<IMultiplayHttpClientFactory, MultiplayHttpClientFactory>();
    }
}

public class MultiplayAllocator(ILogger<MultiplayAllocator> logger, IMultiplayHttpClientFactory httpClientFactory) : IMatchmakerAllocator
{
    // Configuration - users should modify these constants for their setup
    private const string FleetId = "your_fleet_id";
    private const int BuildConfigId = 0;
    private const string DefaultRegion = "your_default_region";

    // Service constants
    private const string MultiplayHost = "multiplay.services.api.unity.com";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var createAllocationUrl = $"https://{MultiplayHost}/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultRegion;

        using var client = httpClientFactory.Create(context.ServiceToken);

        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(new MultiplayAllocateRequest()
            {
                AllocationId = Guid.NewGuid().ToString(),
                BuildConfigurationId = BuildConfigId,
                RegionId = region,
                Payload = JsonConvert.SerializeObject(request.MatchmakingResults)
            }), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(createAllocationUrl, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error allocating Multiplay {error}", responseContent);

                return new AllocateResponse(AllocateStatus.Error)
                {
                    Message = responseContent
                };
            }

            var multiplayAllocation = JsonConvert.DeserializeObject<MultiplayAllocateResponse>(responseContent);

            return new AllocateResponse(AllocateStatus.Created)
            {
                AllocationData = new Dictionary<string, object>
                {
                    { "allocationId", multiplayAllocation?.AllocationId ?? string.Empty },
                    { "region", region }
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error allocating Multiplay");

            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = ex.Message
            };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var allocationId = request.AllocationData["allocationId"].ToString();
        var getAllocationsUrl = $"https://{MultiplayHost}/v1/allocations/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations/{allocationId}";

        using var client = httpClientFactory.Create(context.ServiceToken);

        try
        {
            var allocation = await client.GetAsync(getAllocationsUrl);

            var responseContent = await allocation.Content.ReadAsStringAsync();
            if (!allocation.IsSuccessStatusCode)
            {
                return new PollResponse(PollStatus.Error)
                {
                    Message = responseContent
                };
            }

            var multiplayAllocation = JsonConvert.DeserializeObject<MultiplayAllocationStatus>(responseContent);

            if (!string.IsNullOrEmpty(multiplayAllocation?.Fulfilled))
            {
                if (!multiplayAllocation.Readiness || !string.IsNullOrEmpty(multiplayAllocation.Ready))
                {
                    if (!string.IsNullOrEmpty(multiplayAllocation.Ipv4) && multiplayAllocation.GamePort != 0)
                    {
                        return new PollResponse(PollStatus.Allocated)
                        {
                            AssignmentData = AssignmentData.IpPort(multiplayAllocation.Ipv4, multiplayAllocation.GamePort),
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling Multiplay");

            return new PollResponse(PollStatus.Error)
            {
                Message = ex.Message
            };
        }

        return new PollResponse(PollStatus.Pending);
    }
}

public interface IMultiplayHttpClientFactory
{
    HttpClient Create(string serviceToken);
}

public class MultiplayHttpClientFactory : IMultiplayHttpClientFactory
{
    public HttpClient Create(string serviceToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        return client;
    }
}

class MultiplayAllocateRequest
{
    [JsonProperty("allocationId")]
    public string? AllocationId { get; set; }
    [JsonProperty("buildConfigurationId")]
    public int BuildConfigurationId { get; set; }
    [JsonProperty("regionId")]
    public string? RegionId { get; set; }
    [JsonProperty("payload")]
    public string? Payload { get; set; }
}

class MultiplayAllocateResponse
{
    [JsonProperty("allocationId")]
    public string? AllocationId { get; set; }
}

class MultiplayAllocationStatus
{
    [JsonProperty("allocationId")]
    public string? AllocationId { get; set; }

    [JsonProperty("fulfilled")]
    public string? Fulfilled { get; set; }

    [JsonProperty("readiness")]
    public bool Readiness { get; set; }

    [JsonProperty("ready")]
    public string? Ready { get; set; }

    [JsonProperty("ipv4")]
    public string? Ipv4 { get; set; }

    [JsonProperty("gamePort")]
    public int GamePort { get; set; }
}
