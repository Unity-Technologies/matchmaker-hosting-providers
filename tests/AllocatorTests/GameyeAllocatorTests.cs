using GameyeAllocatorModule;
using GameyeAllocatorModule.Client;
using GameyeAllocatorModule.Client.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using MatchmakingResults = Unity.Services.CloudCode.Apis.Matchmaker.MatchmakingResults;

namespace AllocatorTests;

public class GameyeAllocatorTests
{
	private readonly Mock<ILogger<GameyeAllocator>> _loggerMock = new();
	private readonly Mock<IGameyeHttpClientFactory> _httpClientFactoryMock = new();
	private readonly Mock<ISecretClient> _secretClientMock = new();
	private readonly Mock<IGameApiClient> _gameClientMock = new();
	private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

	private readonly Mock<IExecutionContext> _executionContextMock = new();

	private readonly GameyeAllocator _allocator;

	public GameyeAllocatorTests()
	{
		_gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
		_secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
			.ReturnsAsync(new Secret("secret"));
		_httpClientFactoryMock.Setup(f => f.Create(It.IsAny<string>()))
			.Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
		_allocator = new GameyeAllocator(_gameClientMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);
	}

	[Test]
	public async Task TestGameyeCanAllocate()
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
				StatusCode = System.Net.HttpStatusCode.Created,
				Content = new StringContent(
					"""
					{
					  "id": "test-session-id",
					  "host": "203.0.113.42",
					  "ports": [
					    { "type": "udp", "container": 7777, "host": 49152 }
					  ]
					}
					"""
				),
			});

		var allocation = await _allocator.Allocate(_executionContextMock.Object,
			new AllocateRequest("match-1234",
				new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
		Assert.That(allocation.Message, Is.Null);
		Assert.That(allocation.AllocationData, Is.Not.Null);
		Assert.That(allocation.AllocationData["sessionId"], Is.EqualTo("test-session-id"));
		Assert.That(allocation.AllocationData["host"], Is.EqualTo("203.0.113.42"));
		Assert.That(allocation.AllocationData["port"], Is.EqualTo(49152));
	}

	[Test]
	public async Task TestGameyeCanAllocateWithMatchMetadata()
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
				StatusCode = System.Net.HttpStatusCode.Created,
				Content = new StringContent(
					"""
					{
					  "id": "match-1234",
					  "host": "203.0.113.42",
					  "ports": [
					    { "type": "udp", "container": 7777, "host": 49152 }
					  ]
					}
					"""
				),
			});

		var allocation = await _allocator.Allocate(_executionContextMock.Object,
			new AllocateRequest("match-1234",
				new MatchmakingResults(null, "matchId", "poolId", "testPool", "queueName", new())));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));

		// Verify the request body sent to Gameye
		Assert.That(capturedRequest, Is.Not.Null);
		Assert.That(capturedRequest!.Content, Is.Not.Null);

		var body = await capturedRequest.Content!.ReadAsStringAsync();
		var sessionRequest = JsonConvert.DeserializeObject<SessionRequest>(body);

		Assert.That(sessionRequest, Is.Not.Null);
		Assert.That(sessionRequest!.Image, Is.EqualTo("MyGame"));
		Assert.That(sessionRequest.Location, Is.EqualTo("europe"));
		Assert.That(sessionRequest.Id, Is.EqualTo("match-1234"));
		Assert.That(sessionRequest.Env, Is.Not.Null);
		Assert.That(sessionRequest.Env!["MATCH_ID"], Is.EqualTo("match-1234"));
		Assert.That(sessionRequest.Labels, Is.Not.Null);
		Assert.That(sessionRequest.Labels!["matchmaker"], Is.EqualTo("unity"));
		Assert.That(sessionRequest.Labels["pool"], Is.EqualTo("testPool"));
	}

	[Test]
	public async Task TestGameyePollReturnsAllocatedFromCachedData()
	{
		// Gameye returns host/port synchronously, so Poll should resolve from AllocationData
		var poll = await _allocator.Poll(_executionContextMock.Object,
			new PollRequest("match-1234",
				new Dictionary<string, object>
				{
					{ "sessionId", "test-session-id" },
					{ "host", "203.0.113.42" },
					{ "port", 49152 },
				},
				DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
		Assert.That(poll.Message, Is.Null);
		Assert.That(poll.AssignmentData, Is.Not.Null);
		Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
		Assert.That(poll.AssignmentData.Ip, Is.EqualTo("203.0.113.42"));
		Assert.That(poll.AssignmentData.Port, Is.EqualTo(49152));
	}

	[Test]
	public async Task TestGameyePollFallsBackToApi()
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
					  "id": "test-session-id",
					  "host": "203.0.113.42",
					  "ports": [
					    { "type": "udp", "container": 7777, "host": 49152 }
					  ],
					  "status": "running"
					}
					"""
				),
			});

		// Simulate missing host/port in allocation data (edge case)
		var poll = await _allocator.Poll(_executionContextMock.Object,
			new PollRequest("match-1234",
				new Dictionary<string, object>
				{
					{ "sessionId", "test-session-id" },
					{ "host", "" },
					{ "port", 0 },
				},
				DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
		Assert.That(poll.AssignmentData, Is.Not.Null);
		Assert.That(poll.AssignmentData.Ip, Is.EqualTo("203.0.113.42"));
		Assert.That(poll.AssignmentData.Port, Is.EqualTo(49152));
	}

	[Test]
	public async Task TestGameyeAllocationError()
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
				StatusCode = System.Net.HttpStatusCode.NotFound,
				Content = new StringContent("Location not found"),
			});

		var allocation = await _allocator.Allocate(_executionContextMock.Object,
			new AllocateRequest("match-1234",
				new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
		Assert.That(allocation.Message, Is.Not.Null);
	}
}
