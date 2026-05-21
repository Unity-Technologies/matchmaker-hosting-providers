using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using GameyeAllocatorModule.Client;
using GameyeAllocatorModule.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using IExecutionContext = Unity.Services.CloudCode.Core.IExecutionContext;

namespace GameyeAllocatorModule;

/// <summary>
/// Module configuration for dependency injection.
/// Edit the <see cref="GameyeAllocatorConfig"/> instance below to configure the allocator
/// for your project — image name, environment, region, ports, and version.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
	public void Setup(ICloudCodeConfig config)
	{
		config.Dependencies.AddSingleton(GameApiClient.Create());
		config.Dependencies.AddScoped<IGameyeHttpClientFactory, GameyeHttpClientFactory>();

		// ──────────────────────────────────────────────────────────────
		// Gameye allocator configuration — edit the values below.
		// ──────────────────────────────────────────────────────────────
		config.Dependencies.AddSingleton(new GameyeAllocatorConfig
		{
			// Required — the application image name registered in the Gameye Admin Panel.
			ImageName = "test_nginx",

			// The API environment. Use Sandbox for development, Production for live.
			Environment = GameyeEnvironment.Sandbox,

			// Default deployment region — used when no pool-to-location mapping matches.
			DefaultLocation = "eu-west",

			// Option A — Unity QoS automatic region selection (recommended).
			// Maps the value Unity puts in MatchProperties["Region"] to a Gameye location.
			// LocationByRegion = new Dictionary<string, string>
			// {
			//     { "eu-west",        "eu-west"        },
			//     { "us-central",     "us-central"     },
			//     { "asia-northeast", "asia-northeast" },
			// },

			// Option B — pool-name mapping (use when Unity QoS is not configured).
			// LocationByPool = new Dictionary<string, string>
			// {
			//     { "eu-west-pool",    "eu-west"        },
			//     { "us-central-pool", "us-central"     },
			//     { "ap-ne-pool",      "asia-northeast" },
			// },

			// Primary game server port (must match your Dockerfile EXPOSE / Admin Panel config).
			GamePort = 80,

			// Optional — pin a specific Docker image tag / version.
			// When null, Gameye uses the highest-priority tag configured in the Admin Panel.
			// Version = "v1.2.3",

			// Optional — additional ports to expose to game clients (e.g. voice, query, RCON).
			// These are returned in AllocationData as "port_{name}" alongside the primary port.
			// AdditionalPorts = new Dictionary<string, int>
			// {
			//     { "query", 27015 },
			//     { "rcon", 27020 },
			// },
		});
	}
}

public class GameyeAllocator(
	IGameApiClient gameApiClient,
	IGameyeHttpClientFactory httpClientFactory,
	GameyeAllocatorConfig allocatorConfig,
	ILogger<GameyeAllocator> logger) : IMatchmakerAllocator
{
	// Secret names — these must match the secrets stored in Unity Dashboard
	private const string GameyeApiTokenSecretName = "GAMEYE_API_TOKEN";

	[CloudCodeFunction("Matchmaker_AllocateServer")]
	public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
	{
		try
		{
			Secret gameyeApiToken = await gameApiClient.SecretManager.GetSecret(context, GameyeApiTokenSecretName);
			using HttpClient client = httpClientFactory.Create(gameyeApiToken.Value);

			var resolvedLocation = ResolveLocation(request.MatchmakingResults);

			var sessionRequest = new SessionRequest
			{
				Id = request.MatchId,
				Location = resolvedLocation,
				Image = allocatorConfig.ImageName,
				Version = allocatorConfig.Version,
				Env = new Dictionary<string, string>
				{
					{ "MATCH_ID", request.MatchId },
				},
				Labels = new Dictionary<string, string>
				{
					{ "matchmaker", "unity" },
					{ "pool", request.MatchmakingResults.PoolName ?? "" },
				},
			};

			var content = new StringContent(JsonConvert.SerializeObject(sessionRequest), Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.PostAsync($"{allocatorConfig.ApiBaseUrl}/session", content);

			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				logger.LogError("Gameye session creation failed with status code {ResponseStatusCode}: {ResponseContent}", response.StatusCode, responseContent);
				return new AllocateResponse(AllocateStatus.Error)
				{
					Message = responseContent,
				};
			}

			var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseContent);

			var allocationData = new Dictionary<string, object>
			{
				{ "sessionId", sessionResponse?.Id ?? request.MatchId },
				{ "host", sessionResponse?.Host ?? string.Empty },
				{ "port", FindPort(sessionResponse?.Ports, allocatorConfig.GamePort) },
				{ "location", resolvedLocation },
			};

			// Include additional named ports so game clients can access them.
			foreach (var (name, containerPort) in allocatorConfig.AdditionalPorts)
			{
				int hostPort = FindPort(sessionResponse?.Ports, containerPort);
				if (hostPort > 0)
				{
					allocationData[$"port_{name}"] = hostPort;
				}
			}

			return new AllocateResponse(AllocateStatus.Created)
			{
				AllocationData = allocationData,
			};
		}
		catch (Exception e)
		{
			logger.LogError(e, "Gameye session creation failed");
			return new AllocateResponse(AllocateStatus.Error)
			{
				Message = e.Message,
			};
		}
	}

	[CloudCodeFunction("Matchmaker_PollAllocation")]
	public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
	{
		var sessionId = request.AllocationData["sessionId"].ToString();

		// Gameye returns host and port synchronously on allocation.
		// If we already have connection data, return it immediately.
		if (request.AllocationData.TryGetValue("host", out var hostObj) &&
		    request.AllocationData.TryGetValue("port", out var portObj))
		{
			var host = hostObj?.ToString();
			var port = Convert.ToInt32(portObj);

			if (!string.IsNullOrEmpty(host) && port > 0)
			{
				return new PollResponse(PollStatus.Allocated)
				{
					AssignmentData = AssignmentData.IpPort(host, port),
				};
			}
		}

		// Fallback: query the session status from the Gameye API
		try
		{
			Secret gameyeApiToken = await gameApiClient.SecretManager.GetSecret(context, GameyeApiTokenSecretName);
			using HttpClient client = httpClientFactory.Create(gameyeApiToken.Value);
			HttpResponseMessage response = await client.GetAsync($"{allocatorConfig.ApiBaseUrl}/session/{sessionId}");
			string responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = responseContent,
				};
			}

			var sessionResponse = JsonConvert.DeserializeObject<SessionResponse>(responseContent);

			if (sessionResponse == null)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = "Session response is null",
				};
			}

			return sessionResponse.Status?.ToLowerInvariant() switch
			{
				"running" => new PollResponse(PollStatus.Allocated)
				{
					AssignmentData = AssignmentData.IpPort(
						sessionResponse.Host ?? string.Empty,
						FindPort(sessionResponse.Ports, allocatorConfig.GamePort)
					),
				},
				"created" or "restarting" => new PollResponse(PollStatus.Pending),
				"exited" or "dead" => new PollResponse(PollStatus.Error)
				{
					Message = $"Session {sessionId} is in state: {sessionResponse.Status}",
				},
				_ => new PollResponse(PollStatus.Pending),
			};
		}
		catch (Exception e)
		{
			logger.LogError(e, "Error polling Gameye session {SessionId}", sessionId);
			return new PollResponse(PollStatus.Error)
			{
				Message = e.Message,
			};
		}
	}

	/// <summary>
	/// Resolves the Gameye location for this match using a three-tier priority:
	/// 1. <c>MatchProperties["Region"]</c> → <see cref="GameyeAllocatorConfig.LocationByRegion"/>
	///    (Unity QoS has already picked the best region — use it directly)
	/// 2. <c>PoolName</c> → <see cref="GameyeAllocatorConfig.LocationByPool"/>
	///    (fallback for studios using per-region pools without QoS)
	/// 3. <see cref="GameyeAllocatorConfig.DefaultLocation"/>
	/// </summary>
	private string ResolveLocation(MatchmakingResults results)
	{
		// Priority 1 — Unity QoS resolved region
		if (results.MatchProperties.TryGetValue("Region", out var regionObj))
		{
			var region = regionObj?.ToString();
			if (!string.IsNullOrEmpty(region) &&
			    allocatorConfig.LocationByRegion.TryGetValue(region, out var regionLocation))
			{
				logger.LogInformation("Region resolved via QoS: MatchProperties[Region]={Region} → {Location}", region, regionLocation);
				return regionLocation;
			}

			if (!string.IsNullOrEmpty(region))
			{
				logger.LogWarning("MatchProperties[Region]={Region} has no entry in LocationByRegion — falling through", region);
			}
		}

		// Priority 2 — pool name mapping
		var pool = results.PoolName;
		if (!string.IsNullOrEmpty(pool) &&
		    allocatorConfig.LocationByPool.TryGetValue(pool, out var poolLocation))
		{
			logger.LogInformation("Region resolved via pool: PoolName={Pool} → {Location}", pool, poolLocation);
			return poolLocation;
		}

		// Priority 3 — static default
		return allocatorConfig.DefaultLocation;
	}

	/// <summary>
	/// Finds the host port mapped to the given container port from the session's port mappings.
	/// Falls back to the first available port if the target port is not found.
	/// </summary>
	private static int FindPort(List<PortMapping>? ports, int containerPort)
	{
		if (ports == null || ports.Count == 0)
			return 0;

		var match = ports.FirstOrDefault(p => p.Container == containerPort);
		return match?.Host ?? ports[0].Host;
	}
}
