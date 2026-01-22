using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;

namespace AgonesAllocatorModule;

/// <summary>
/// Allocator for Agones game server allocation.
/// </summary>
public class AgonesAllocator(ILogger<AgonesAllocator> logger) : IMatchmakerAllocator
{
    // =============================================================================
    // TODO: Configure these values for your Agones setup
    // =============================================================================
    
    /// <summary>
    /// The base URL of your Agones Allocator Service.
    /// This is typically the external IP/hostname of your allocator service.
    /// Example: "https://your-allocator-ip" or "https://allocator.your-domain.com"
    /// </summary>
    private const string AllocatorServiceUrl = "https://YOUR_ALLOCATOR_IP_OR_HOSTNAME";
    
    /// <summary>
    /// The Kubernetes namespace where your Agones fleet is deployed.
    /// Default is "default" but you may have a custom namespace.
    /// </summary>
    private const string AgonesNamespace = "default";
    
    /// <summary>
    /// The name of your Agones fleet to allocate game servers from.
    /// This should match the metadata.name of your Fleet resource in Kubernetes.
    /// </summary>
    private const string FleetName = "YOUR_FLEET_NAME";
    
    /// <summary>
    /// Set to true if your allocator uses a self-signed certificate.
    /// Set to false for production environments with valid SSL certificates.
    /// </summary>
    private const bool BypassSslValidation = true;
    
    /// <summary>
    /// Timeout for allocation requests in seconds.
    /// </summary>
    private const int RequestTimeoutSeconds = 30;
    
    // =============================================================================
    // End of configuration
    // =============================================================================

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        try
        {
            var handler = new HttpClientHandler();
            if (BypassSslValidation)
            {
                handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
            }
            
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
            };
            
            // Build allocation request payload
            // TODO: Customize the gameServerSelectors to match your fleet labels
            // You can add multiple selectors or change the match criteria as needed
            // See: https://agones.dev/site/docs/reference/gameserverallocation/
            var payload = new StringContent(
                $"{{\"namespace\":\"{AgonesNamespace}\",\"gameServerSelectors\":[{{\"matchLabels\":{{\"agones.dev/fleet\":\"{FleetName}\"}}}}]}}",
                System.Text.Encoding.UTF8,
                "application/json"
            );
            
            var response = await httpClient.PostAsync($"{AllocatorServiceUrl}/gameserverallocation", payload);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Allocation failed with status {StatusCode}: {Response}", 
                    (int)response.StatusCode, responseBody);
                return new AllocateResponse(AllocateStatus.Error)
                {
                    Message = $"Allocation failed: {response.StatusCode} - {responseBody}"
                };
            }
            
            // Parse the allocation response
            var jsonDoc = System.Text.Json.JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;
            
            string ip = null;
            int? port = null;
            
            if (root.TryGetProperty("address", out var addressProp))
            {
                ip = addressProp.GetString();
            }
            if (root.TryGetProperty("ports", out var portsProp) && portsProp.GetArrayLength() > 0)
            {
                port = portsProp[0].GetProperty("port").GetInt32();
            }
            
            if (string.IsNullOrEmpty(ip) || !port.HasValue)
            {
                logger.LogError("Allocation did not return a valid IP or Port. Response: {Response}", responseBody);
                return new AllocateResponse(AllocateStatus.Error)
                {
                    Message = "Allocation did not return a valid IP or Port"
                };
            }
            
            logger.LogInformation("Successfully allocated game server at {IP}:{Port}", ip, port.Value);
            
            return new AllocateResponse(AllocateStatus.Created)
            {
                AllocationData = new Dictionary<string, object>
                {
                    { "ip", ip },
                    { "port", port.Value }
                }
            };
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Allocation request timed out after {Timeout} seconds", RequestTimeoutSeconds);
            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"Allocation request timed out after {RequestTimeoutSeconds} seconds"
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating Agones allocation");
            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"Error creating Agones allocation: {e.Message}"
            };
        }
    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        var ip = (string)request.AllocationData["ip"];
        var port = Convert.ToInt32(request.AllocationData["port"]);
        
        return Task.FromResult(new PollResponse(PollStatus.Allocated)
        {
            AssignmentData = AssignmentData.IpPort(ip, port)
        });
    }
}