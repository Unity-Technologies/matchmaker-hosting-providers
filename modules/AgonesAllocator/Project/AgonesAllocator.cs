using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AgonesAllocatorModule.Client;
using AgonesAllocatorModule.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;

namespace AgonesAllocatorModule;

/// <summary>
/// Module configuration for dependency injection.
/// Registers IRequestAdapter
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
    // Configuration - users should modify these constants for their setup
    private const string AllocatorServiceBaseUrl = "AGONES_BASE_URL"; // TODO: Replace with Agones Allocator Service URL

    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddScoped<IRequestAdapter>(_ =>
        {
            // TODO: Replace with required auth of your service
            var authProvider = new AnonymousAuthenticationProvider();

            return new HttpClientRequestAdapter(authProvider)
            {
                BaseUrl = AllocatorServiceBaseUrl
            };
        });
    }
}

/// <summary>
/// Allocator for Agones.
/// </summary>
public class AgonesAllocator(IRequestAdapter requestAdapter, ILogger<AgonesAllocator> logger) : IMatchmakerAllocator
{

    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        var client = new AgonesClient(requestAdapter);

        try
        {
            var allocation = await client.Gameserverallocation.PostAsync(new AllocationAllocationRequest
            {
                // TODO: Add allocation selectors as needed
            });

            var ip = allocation?.Addresses?.FirstOrDefault()?.Address;
            var port = allocation?.Ports?.FirstOrDefault()?.Port;

            if (ip == null || port == null)
            {
                logger.LogError("Allocation did not return a valid IP or Port");
                return new AllocateResponse(AllocateStatus.Error)
                {
                    Message = "Allocation did not return a valid IP or Port"
                };
            }

            return new AllocateResponse(AllocateStatus.Created)
            {
                AllocationData = new Dictionary<string, object>
                {
                    { "ip", ip },
                    { "port", port }
                }
            };
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error creating Agones allocation");

            return new AllocateResponse(AllocateStatus.Error)
            {
                Message = $"Error creating Agones allocation: {e}"
            };
        }

    }

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        return Task.FromResult(new PollResponse(PollStatus.Allocated)
        {
            AssignmentData = AssignmentData.IpPort((string)request.AllocationData["ip"], (int)request.AllocationData["port"])
        });
    }
}
