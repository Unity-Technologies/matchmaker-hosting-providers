using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Newtonsoft.Json;

namespace RocketScienceAllocatorModule;

public class ModuleConfig : ICloudCodeSetup
{
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
        config.Dependencies.AddScoped<IRocketScienceHttpClientFactory, RocketScienceHttpClientFactory>();
    }
}

public class RocketScienceAllocator(IGameApiClient gameApiClient, IRocketScienceHttpClientFactory httpClientFactory, ILogger<RocketScienceAllocator> logger) : IMatchmakerAllocator
{
    // Configuration - users should modify these constants for their setup
    private const string FleetId = "your_fleet_id";
    private const int BuildConfigId = 0;
    private const string DefaultRegion = "your_default_region";

    // TODO(dr): need a way to override project and env ID.

    // Service constants
    private const string BaseUrl = "https://api.multiplay.dev";

    // Secret names - these must match the secrets stored in Unity Dashboard
    private const string RocketScienceMultiplayApiKey = "ROCKET_SCIENCE_MULTIPLAY_API_KEY";

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var processAllocationUrl = $"{BaseUrl}/v4/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations";
        var region = request.MatchmakingResults.MatchProperties.GetValueOrDefault("region")?.ToString() ?? DefaultRegion;

        try
        {
            var apiKey = await gameApiClient.SecretManager.GetSecret(context, RocketScienceMultiplayApiKey);

            using var client = httpClientFactory.Create(apiKey.Value);

            var content = new StringContent(JsonConvert.SerializeObject(new ProcessAllocationRequest()
            {
                AllocationId = Guid.NewGuid().ToString(),
                BuildConfigurationId = BuildConfigId,
                RegionId = region,
                Payload = JsonConvert.SerializeObject(request.MatchmakingResults)
            }), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(processAllocationUrl, content);

            var responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error processing allocation {error}", responseContent);

                return new AllocateResponse(AllocateStatus.Error)
                {
                    Message = responseContent
                };
            }

            var processedAllocation = JsonConvert.DeserializeObject<ProcessAllocationResponse>(responseContent);

            return new AllocateResponse(AllocateStatus.Created)
            {
                AllocationData = new Dictionary<string, object>
                {
                    { "allocationId", processedAllocation?.AllocationId ?? string.Empty },
                    { "region", region }
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing allocation");

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
        var getAllocationUrl = $"{BaseUrl}/v4/projects/{context.ProjectId}/environments/{context.EnvironmentId}/fleets/{FleetId}/allocations/{allocationId}";

        try
        {
            var apiKey = await gameApiClient.SecretManager.GetSecret(context, RocketScienceMultiplayApiKey);

            using var client = httpClientFactory.Create(apiKey.Value);

            var allocation = await client.GetAsync(getAllocationUrl);

            var responseContent = await allocation.Content.ReadAsStringAsync();
            if (!allocation.IsSuccessStatusCode)
            {
                return new PollResponse(PollStatus.Error)
                {
                    Message = responseContent
                };
            }

            var allocationStatus = JsonConvert.DeserializeObject<AllocationStatus>(responseContent);

            // Game servers can optionally support a "readiness" state, allowing it to indicate to the matchmaker that
            // it's ready to accept players. If the server indicates it's not ready, we should continue polling until
            // it is.
            var serverNotReady = allocationStatus?.Readiness == true && string.IsNullOrEmpty(allocationStatus?.Ready);

            if (string.IsNullOrEmpty(allocationStatus?.Fulfilled) || serverNotReady)
            {
                return new PollResponse(PollStatus.Pending);
            }

            if (!string.IsNullOrEmpty(allocationStatus?.Ipv4) && allocationStatus.GamePort != 0)
            {
                return new PollResponse(PollStatus.Allocated)
                {
                    AssignmentData = AssignmentData.IpPort(allocationStatus.Ipv4, allocationStatus.GamePort),
                };
            }

            return new PollResponse(PollStatus.Pending);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error polling allocation");

            return new PollResponse(PollStatus.Error)
            {
                Message = ex.Message
            };
        }
    }
}

public interface IRocketScienceHttpClientFactory
{
    HttpClient Create(string apiKey);
}

public class RocketScienceHttpClientFactory : IRocketScienceHttpClientFactory
{
    public HttpClient Create(string apiKey)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }
}

class ProcessAllocationRequest
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

class ProcessAllocationResponse
{
    [JsonProperty("allocationId")]
    public string? AllocationId { get; set; }
}

class AllocationStatus
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
