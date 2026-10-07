using EdgegapAllocatorModule;
using EdgegapAllocatorModule.Client;
using EdgegapAllocatorModule.Client.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using MatchmakingResults = Unity.Services.CloudCode.Apis.Matchmaker.MatchmakingResults;

namespace AllocatorTests;

public class EdgegapAllocatorTests
{
	private readonly Mock<ILogger<EdgegapAllocator>> _loggerMock = new();
	private readonly Mock<IEdgegapHttpClientFactory> _httpClientFactoryMock = new();
	private readonly Mock<ISecretClient> _secretClientMock = new();
	private readonly Mock<IGameApiClient> _gameClientMock = new();
	private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

	private readonly Mock<IExecutionContext> _executionContextMock = new();

	private readonly EdgegapAllocator _allocator;

	public EdgegapAllocatorTests()
	{
		_gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
		_secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
			.ReturnsAsync(new Secret("secret"));
		_httpClientFactoryMock.Setup(f => f.Create(It.IsAny<string>()))
			.Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
		_allocator = new EdgegapAllocator(_gameClientMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);
	}

	[Test]
	public async Task TestEdgegapCanAllocate()
	{
		_httpMessageHandlerMock.Reset();
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage()
			{
				Content = new StringContent("{'request_id': 'request_id'}"),
			});

		var allocation = await _allocator.Allocate(_executionContextMock.Object,
			new AllocateRequest("1234",
				new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
		Assert.That(allocation.Message, Is.Null);
		Assert.That(allocation.AllocationData, Is.Not.Null);
		Assert.That(allocation.AllocationData["requestId"], Is.EqualTo("request_id"));
	}

	[Test]
	public async Task TestThatEdgegapCanAllocatePlayersIps()
	{
		HttpRequestMessage? capturedRequest = null;
		_httpMessageHandlerMock.Reset();
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.Callback<HttpRequestMessage, CancellationToken>((req, _) =>
			{
				capturedRequest = req;
			})
			.ReturnsAsync(new HttpResponseMessage()
			{
				Content = new StringContent("{'request_id': 'request_id'}"),
			});

		var matchProperties = new Dictionary<string, object>
		{
			{
				"Players", new JArray(
					new JObject
					{
						["Id"] = "player1",
						["CustomData"] = new JObject
						{
							["player_ip"] = "127.0.0.1",
						},
					}
				)
			},
		};

		var allocation = await _allocator.Allocate(_executionContextMock.Object,
			new AllocateRequest("1234",
				new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", matchProperties)));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
		Assert.That(allocation.Message, Is.Null);
		Assert.That(allocation.AllocationData, Is.Not.Null);
		Assert.That(allocation.AllocationData["requestId"], Is.EqualTo("request_id"));

		// Check the body sent to Edgegap
		Assert.That(capturedRequest, Is.Not.Null);
		Assert.That(capturedRequest!.Content, Is.Not.Null);

		var body = await capturedRequest.Content.ReadAsStringAsync();

		var deploymentRequest =
			JsonConvert.DeserializeObject<DeploymentRequest>(body);

		Assert.That(deploymentRequest, Is.Not.Null);
		Assert.That(deploymentRequest!.Application, Is.EqualTo("MyApp"));
		Assert.That(deploymentRequest.Version, Is.EqualTo("MyVersion"));
		Assert.That(deploymentRequest.Users, Is.Not.Null);
		Assert.That(deploymentRequest.Users, Has.Count.EqualTo(1));

		var user = deploymentRequest.Users![0];

		Assert.That(user.UserType, Is.EqualTo("ip_address"));
		Assert.That(user.UserData.IpAddress, Is.EqualTo("127.0.0.1"));

		var env = deploymentRequest.EnvironmentVariables![0];

		Assert.That(env.Key, Is.EqualTo("MATCH_ID"));
		Assert.That(env.Value, Is.EqualTo("1234"));
	}

	[Test]
	public async Task TestEdgegapCanPoll()
	{
		_httpMessageHandlerMock.Reset();
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				Content = new StringContent(
					"""
					{
					  "request_id": "request_id",
					  "current_status": "Status.READY",
					  "running": true,
					  "error": false,
					  "public_ip": "127.0.0.1",
					  "ports": {
					    "gameport": {
					      "external": 31234,
					      "internal": 7777,
					      "protocol": "udp",
					      "name": "gameport"
					    }
					  },
					  "max_duration": 3600
					}
					"""
				)
			});
		var poll = await _allocator.Poll(_executionContextMock.Object,
			new PollRequest("1234",
				new Dictionary<string, object>
				{
					{
						"requestId", "request_id"
					},
				},
				DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
		Assert.That(poll.Message, Is.Null);
		Assert.That(poll.AssignmentData, Is.Not.Null);
		Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
		Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
		Assert.That(poll.AssignmentData.Port, Is.EqualTo(31234));
	}

	[Test]
	public async Task TestEdgegapPollErrors()
	{
		_httpMessageHandlerMock.Reset();
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(new HttpResponseMessage
			{
				Content = new StringContent(
					"""
					{
					  "request_id": "request_id",
					  "current_status": "Status.ERROR",
					  "running": false,
					  "error": true,
					  "public_ip": "127.0.0.1",
					  "ports": {
					    "gameport": {
					      "external": 31234,
					      "internal": 7777,
					      "protocol": "udp",
					      "name": "gameport"
					    }
					  },
					  "max_duration": 3600
					}
					"""
				)
			});
		var poll = await _allocator.Poll(_executionContextMock.Object,
			new PollRequest("1234",
				new Dictionary<string, object>
				{
					{
						"requestId", "request_id"
					},
				},
				DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Error));
		Assert.That(poll.Message, Is.Not.Null);
	}
}