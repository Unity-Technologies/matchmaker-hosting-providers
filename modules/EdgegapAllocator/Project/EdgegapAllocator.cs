using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EdgegapAllocatorModule.Client;
using EdgegapAllocatorModule.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using Unity.Services.Matchmaker.Model;
using IExecutionContext = Unity.Services.CloudCode.Core.IExecutionContext;

namespace EdgegapAllocatorModule;

/// <summary>
/// Module configuration for dependency injection.
/// Registers IGameApiClient as a singleton for accessing Unity services like Secret Manager.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
{
	public void Setup(ICloudCodeConfig config)
	{
		config.Dependencies.AddSingleton(GameApiClient.Create());
		config.Dependencies.AddScoped<IEdgegapHttpClientFactory, EdgegapHttpClientFactory>();
	}
}

public class EdgegapAllocator(IGameApiClient gameApiClient, IEdgegapHttpClientFactory httpClientFactory, ILogger<EdgegapAllocator> logger) : IMatchmakerAllocator
{
	// Configuration - users should modify these constants for their setup
	private const string ApplicationName = "MyApp"; // TODO: Replace with actual application name
	private const string VersionName = "MyVersion"; // TODO: Replace with actual version name
	private const string PortName = "gameport"; // TODO: Replace with actual port name

	// Edgegap Constants
	private const string EdgegapApiUrl = "https://api.edgegap.com";
	private const bool ForceCachedLocation = false; // You need to activate a cached application version in Edgegap to use this feature

	// Secret names - these must match the secrets stored in Unity Dashboard
	private const string EdgegapApiTokenSecretName = "EDGEGAP_API_TOKEN";

	[CloudCodeFunction("Matchmaker_AllocateServer")]
	public async Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
	{
		try
		{
			Secret edgegapApiToken = await gameApiClient.SecretManager.GetSecret(context, EdgegapApiTokenSecretName);
			using HttpClient client = httpClientFactory.Create(edgegapApiToken.Value);

			var playerIps = ExtractValidPlayerIps(request.MatchmakingResults.MatchProperties);

			var users = playerIps
				.Select(ip => new DeploymentUser
				{
					UserType = "ip_address",
					UserData = new UserData
					{
						IpAddress = ip.ToString(),
					},
				})
				.ToList();

			if (users.Count == 0)
			{
				// If no valid player IPs were found, add a placeholder user with geo coordinates
				logger.LogWarning("No valid player IPs found in match properties, adding dummy user with geo coordinates, please add player_ip to player custom data");
				users.Add(new DeploymentUser
				{
					UserType = "geo_coordinates",
					UserData = new UserData
					{
						Latitude = 35.891111,
						Longitude = -106.263889,
					},
				});
			}

			// You can add additional deployment parameters here like tags (limited to 20 characters) or environment variables
			var deploymentRequest = new DeploymentRequest
			{
				Application = ApplicationName,
				Version = VersionName,
				RequireCachedLocations = ForceCachedLocation,
				Users = users,
				EnvironmentVariables =
				[
					new EnvironmentVariable
					{
						Key = "MATCH_ID",
						Value = request.MatchId,
						IsHidden = false,
					},
				],
				Tags = ["ugs-matchmaker"],
			};

			var content = new StringContent(JsonConvert.SerializeObject(deploymentRequest), Encoding.UTF8, "application/json");
			HttpResponseMessage response = await client.PostAsync($"{EdgegapApiUrl}/v2/deployments", content);

			string responseContent = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				logger.LogError("Edgegap deployment failed with status code {ResponseStatusCode}: {ResponseContent}", response.StatusCode, responseContent);
				return new AllocateResponse(AllocateStatus.Error)
				{
					Message = responseContent
				};
			}

			var edgegapDeployment = JsonConvert.DeserializeObject<DeploymentResponse>(responseContent);

			return new AllocateResponse(AllocateStatus.Created)
			{
				AllocationData = new Dictionary<string, object>
				{
					{
						"requestId", edgegapDeployment?.RequestId ?? string.Empty
					},
				},
			};
		}
		catch (Exception e)
		{
			logger.LogError(e, "Edgegap deployment failed");
			return new AllocateResponse(AllocateStatus.Error)
			{
				Message = e.Message,
			};
		}
	}

	[CloudCodeFunction("Matchmaker_PollAllocation")]
	public async Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
	{
		var requestId = request.AllocationData["requestId"].ToString();
		try
		{
			Secret edgegapApiToken = await gameApiClient.SecretManager.GetSecret(context, EdgegapApiTokenSecretName);
			HttpClient client = httpClientFactory.Create(edgegapApiToken.Value);
			HttpResponseMessage response = await client.GetAsync($"{EdgegapApiUrl}/v1/status/{requestId}");
			string responseContent = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = responseContent,
				};
			}

			var deploymentStatus = JsonConvert.DeserializeObject<DeploymentStatusResponse>(responseContent);

			if (deploymentStatus == null)
			{
				return new PollResponse(PollStatus.Error)
				{
					Message = "Deployment status response is null",
				};
			}

			switch (deploymentStatus.CurrentStatus)
			{
				case DeploymentStatus.Ready:
					{
						if (deploymentStatus.Ports == null || deploymentStatus.Ports.Count == 0)
						{
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Deployment has no exposed ports. Response: {responseContent}",
							};
						}

						if (!deploymentStatus.Ports.TryGetValue(PortName, out var port))
						{
		
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Requested port {PortName} is not exposed by the deployment. Please verify the name of the port in the Edgegap dashboard."
							};
						}

						if (deploymentStatus.PublicIp == null)
						{
							return new PollResponse(PollStatus.Error)
							{
								Message = $"Deployment is READY but public_ip is missing. Response: {responseContent}",
							};
						}

						return new PollResponse(PollStatus.Allocated)
						{
							AssignmentData = AssignmentData.IpPort(
								deploymentStatus.PublicIp,
								port.External
							),
						};
					}
				case DeploymentStatus.Error:
					return new PollResponse(PollStatus.Error)
					{
						Message = $"Deployment failed with the current error: {deploymentStatus.ErrorDetail}",
					};
				case DeploymentStatus.Terminating:
				case DeploymentStatus.Terminated:
					return new PollResponse(PollStatus.Error)
					{
						Message = "Deployment is terminated or terminated and can't receive connection anymore",
					};
				case DeploymentStatus.Deploying:
				case DeploymentStatus.Seeking:
				default:
					return new PollResponse(PollStatus.Pending);
			}
		}
		catch (Exception e)
		{
			logger.LogError(e, "Error polling Edgegap");
			return new PollResponse(PollStatus.Error)
			{
				Message = e.Message,
			};
		}
	}


	/// <summary>
	/// Safely extracts all valid player IP addresses from Matchmaker match properties under the "player_ip" custom data key.
	/// </summary>
	private IReadOnlyList<IPAddress> ExtractValidPlayerIps(
		Dictionary<string, object> matchProperties
	)
	{
		var result = new List<IPAddress>();

		if (!matchProperties.TryGetValue("Players", out var playersObj))
		{
			logger.LogDebug("No Players key found in MatchProperties");
			return result;
		}

		if (playersObj is not JArray playersArray)
		{
			logger.LogWarning("Players is not a JArray (actual type: {type})", playersObj.GetType());
			return result;
		}

		List<Player>? players;
		try
		{
			players = playersArray.ToObject<List<Player>>();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to deserialize Players array");
			return result;
		}

		if (players == null)
			return result;

		foreach (var player in players)
		{
			if (player.CustomData is not JObject customData)
				continue;

			if (!customData.TryGetValue("player_ip", out var ipToken))
				continue;

			var ipString = ipToken.Type switch
			{
				JTokenType.String => ipToken.Value<string>(),
				_ => null
			};

			if (string.IsNullOrWhiteSpace(ipString))
				continue;

			if (IPAddress.TryParse(ipString, out var ip))
			{
				result.Add(ip);
			}
			else
			{
				logger.LogDebug("Invalid IP format ignored: {ip}", ipString);
			}
		}

		return result;
	}
}