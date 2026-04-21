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
/// Registers IGameApiClient as a singleton for accessing Unity services like Secret Manager.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
	public void Setup(ICloudCodeConfig config)
	{
		config.Dependencies.AddSingleton(GameApiClient.Create());
		config.Dependencies.AddScoped<IGameyeHttpClientFactory, GameyeHttpClientFactory>();
	}
}

public class GameyeAllocator(IGameApiClient gameApiClient, IGameyeHttpClientFactory httpClientFactory, ILogger<GameyeAllocator> logger) : IMatchmakerAllocator
{
	// Configuration - users should modify these constants for their setup
	private const string ImageName = "MyGame"; // TODO: Replace with your Gameye application image name
	private const string DefaultLocation = "europe"; // TODO: Replace with your preferred region
	private const int GamePort = 7777; // TODO: Replace with your game server port

	// Gameye Constants
	private const string GameyeApiUrl = "https://api.gameye.io";

	// Secret names - these must match the secrets stored in Unity Dashboard
	private const string GameyeApiTokenSecretName = "GAMEYE_API_TOKEN";

	[CloudCodeFunction("Matchmaker_AllocateServer")]
	public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
	{
		try
		{
			Secret gameyeApiToken = await gameApiClient.SecretManager.GetSecret(context, GameyeApiTokenSecretName);
			using HttpClient client = httpClientFactory.Create(gameyeApiToken.Value);

			var sessionRequest = new SessionRequest
			{
				Id = request.MatchId,
				Location = DefaultLocation,
				Image = ImageName,
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
			HttpResponseMessage response = await client.PostAsync($"{GameyeApiUrl}/session", content);

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

			return new AllocateResponse(AllocateStatus.Created)
			{
				AllocationData = new Dictionary<string, object>
				{
					{ "sessionId", sessionResponse?.Id ?? request.MatchId },
					{ "host", sessionResponse?.Host ?? string.Empty },
					{ "port", FindGamePort(sessionResponse?.Ports) },
				},
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
			HttpResponseMessage response = await client.GetAsync($"{GameyeApiUrl}/session/{sessionId}");
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
						FindGamePort(sessionResponse.Ports)
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
	/// Finds the host port mapped to the configured game port from the session's port mappings.
	/// Falls back to the first available port if the configured port is not found.
	/// </summary>
	private static int FindGamePort(List<PortMapping>? ports)
	{
		if (ports == null || ports.Count == 0)
			return 0;

		var match = ports.FirstOrDefault(p => p.Container == GamePort);
		return match?.Host ?? ports[0].Host;
	}
}
