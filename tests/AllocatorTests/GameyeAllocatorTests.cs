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

	private GameyeAllocator CreateAllocator(GameyeAllocatorConfig? config = null)
	{
		config ??= new GameyeAllocatorConfig
		{
			ImageName = "MyGame",
			Environment = GameyeEnvironment.Sandbox,
			DefaultLocation = "europe",
			GamePort = 7777,
		};
		return new GameyeAllocator(_gameClientMock.Object, _httpClientFactoryMock.Object, config, _loggerMock.Object);
	}

	[SetUp]
	public void SetUp()
	{
		_httpMessageHandlerMock.Reset();
		_gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
		_secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
			.ReturnsAsync(new Secret("secret"));
		_httpClientFactoryMock.Setup(f => f.Create(It.IsAny<string>()))
			.Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
	}

	private void SetupHttpResponse(HttpResponseMessage response)
	{
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.ReturnsAsync(response);
	}

	private void SetupHttpResponseWithCapture(HttpResponseMessage response, Action<HttpRequestMessage> capture)
	{
		_httpMessageHandlerMock
			.Protected()
			.Setup<Task<HttpResponseMessage>>(
				"SendAsync",
				ItExpr.IsAny<HttpRequestMessage>(),
				ItExpr.IsAny<CancellationToken>())
			.Callback<HttpRequestMessage, CancellationToken>((req, _) => capture(req))
			.ReturnsAsync(response);
	}

	private static HttpResponseMessage SuccessResponse(string id = "test-session-id", string host = "203.0.113.42", int containerPort = 7777, int hostPort = 49152) =>
		new()
		{
			StatusCode = System.Net.HttpStatusCode.Created,
			Content = new StringContent(JsonConvert.SerializeObject(new SessionResponse
			{
				Id = id,
				Host = host,
				Ports = [new PortMapping { Type = "udp", Container = containerPort, Host = hostPort }],
			})),
		};

	private static AllocateRequest MakeAllocateRequest(
		string matchId = "match-1234",
		string poolName = "poolName",
		Dictionary<string, object>? matchProperties = null) =>
		new(matchId, new MatchmakingResults(null, "matchId", "poolId", poolName, "queueName",
			matchProperties ?? new Dictionary<string, object>()));

	// ── Basic allocation ──────────────────────────────────────────────

	[Test]
	public async Task TestGameyeCanAllocate()
	{
		SetupHttpResponse(SuccessResponse());
		var allocation = await CreateAllocator().Allocate(_executionContextMock.Object, MakeAllocateRequest());

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
		Assert.That(allocation.AllocationData, Is.Not.Null);
		Assert.That(allocation.AllocationData["sessionId"], Is.EqualTo("test-session-id"));
		Assert.That(allocation.AllocationData["host"], Is.EqualTo("203.0.113.42"));
		Assert.That(allocation.AllocationData["port"], Is.EqualTo(49152));
	}

	[Test]
	public async Task TestGameyeCanAllocateWithMatchMetadata()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(id: "match-1234"), req => capturedRequest = req);

		var allocation = await CreateAllocator().Allocate(_executionContextMock.Object,
			MakeAllocateRequest(matchId: "match-1234", poolName: "testPool"));

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
		Assert.That(capturedRequest, Is.Not.Null);

		var body = JsonConvert.DeserializeObject<SessionRequest>(
			await capturedRequest!.Content!.ReadAsStringAsync());

		Assert.That(body!.Image, Is.EqualTo("MyGame"));
		Assert.That(body.Location, Is.EqualTo("europe"));
		Assert.That(body.Id, Is.EqualTo("match-1234"));
		Assert.That(body.Env!["MATCH_ID"], Is.EqualTo("match-1234"));
		Assert.That(body.Labels!["matchmaker"], Is.EqualTo("unity"));
		Assert.That(body.Labels["pool"], Is.EqualTo("testPool"));
	}

	// ── Environment URL selection ─────────────────────────────────────

	[Test]
	public async Task TestGameyeUsesSandboxUrl()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		await CreateAllocator(new GameyeAllocatorConfig { ImageName = "g", Environment = GameyeEnvironment.Sandbox })
			.Allocate(_executionContextMock.Object, MakeAllocateRequest());

		Assert.That(capturedRequest!.RequestUri!.ToString(),
			Does.StartWith("https://api.sandbox-gameye.gameye.net/session"));
	}

	[Test]
	public async Task TestGameyeUsesProductionUrl()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		await CreateAllocator(new GameyeAllocatorConfig { ImageName = "g", Environment = GameyeEnvironment.Production })
			.Allocate(_executionContextMock.Object, MakeAllocateRequest());

		Assert.That(capturedRequest!.RequestUri!.ToString(),
			Does.StartWith("https://api-production-gameye.gameye.net/session"));
	}

	// ── Version field ─────────────────────────────────────────────────

	[Test]
	public async Task TestGameyeSendsVersionWhenConfigured()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		await CreateAllocator(new GameyeAllocatorConfig { ImageName = "g", Version = "v2.1.0" })
			.Allocate(_executionContextMock.Object, MakeAllocateRequest());

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Version, Is.EqualTo("v2.1.0"));
	}

	[Test]
	public async Task TestGameyeOmitsVersionWhenNull()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		await CreateAllocator(new GameyeAllocatorConfig { ImageName = "g", Version = null })
			.Allocate(_executionContextMock.Object, MakeAllocateRequest());

		var rawBody = await capturedRequest!.Content!.ReadAsStringAsync();
		Assert.That(rawBody, Does.Not.Contain("\"version\""));
	}

	// ── Additional ports ──────────────────────────────────────────────

	[Test]
	public async Task TestGameyeIncludesAdditionalPorts()
	{
		SetupHttpResponse(new HttpResponseMessage
		{
			StatusCode = System.Net.HttpStatusCode.Created,
			Content = new StringContent(JsonConvert.SerializeObject(new SessionResponse
			{
				Id = "s",
				Host = "1.2.3.4",
				Ports =
				[
					new PortMapping { Type = "udp", Container = 7777, Host = 25100 },
					new PortMapping { Type = "tcp", Container = 27015, Host = 25101 },
				],
			})),
		});

		var config = new GameyeAllocatorConfig
		{
			ImageName = "g",
			GamePort = 7777,
			AdditionalPorts = new Dictionary<string, int> { { "query", 27015 } },
		};

		var result = await CreateAllocator(config).Allocate(_executionContextMock.Object, MakeAllocateRequest());

		Assert.That(result.AllocationData!["port"], Is.EqualTo(25100));
		Assert.That(result.AllocationData["port_query"], Is.EqualTo(25101));
	}

	// ── Region selection ──────────────────────────────────────────────

	[Test]
	public async Task TestGameyeUsesPoolLocation()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		var config = new GameyeAllocatorConfig
		{
			ImageName = "g",
			DefaultLocation = "europe",
			LocationByPool = new Dictionary<string, string> { { "us-central-pool", "us-central" } },
		};

		await CreateAllocator(config).Allocate(_executionContextMock.Object,
			MakeAllocateRequest(poolName: "us-central-pool"));

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Location, Is.EqualTo("us-central"));
	}

	[Test]
	public async Task TestGameyeUsesQosRegion()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		var config = new GameyeAllocatorConfig
		{
			ImageName = "g",
			DefaultLocation = "europe",
			LocationByRegion = new Dictionary<string, string> { { "us-central", "us-central" } },
		};

		await CreateAllocator(config).Allocate(_executionContextMock.Object,
			MakeAllocateRequest(matchProperties: new Dictionary<string, object> { { "Region", "us-central" } }));

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Location, Is.EqualTo("us-central"));
	}

	[Test]
	public async Task TestGameyeQosRegionTakesPriorityOverPool()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		var config = new GameyeAllocatorConfig
		{
			ImageName = "g",
			DefaultLocation = "europe",
			LocationByRegion = new Dictionary<string, string> { { "ap-ne", "asia-northeast" } },
			LocationByPool = new Dictionary<string, string> { { "eu-west-pool", "eu-west" } },
		};

		await CreateAllocator(config).Allocate(_executionContextMock.Object,
			MakeAllocateRequest(poolName: "eu-west-pool",
				matchProperties: new Dictionary<string, object> { { "Region", "ap-ne" } }));

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Location, Is.EqualTo("asia-northeast"));
	}

	[Test]
	public async Task TestGameyeFallsBackToDefaultLocation()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		await CreateAllocator(new GameyeAllocatorConfig { ImageName = "g", DefaultLocation = "europe" })
			.Allocate(_executionContextMock.Object, MakeAllocateRequest());

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Location, Is.EqualTo("europe"));
	}

	[Test]
	public async Task TestGameyeAllocatorUsesDefaultRegionWhenRegionIsEmptyString()
	{
		HttpRequestMessage? capturedRequest = null;
		SetupHttpResponseWithCapture(SuccessResponse(), req => capturedRequest = req);

		var config = new GameyeAllocatorConfig
		{
			ImageName = "g",
			DefaultLocation = "europe",
			LocationByRegion = new Dictionary<string, string> { { "eu-west", "eu-west" } },
		};

		await CreateAllocator(config).Allocate(_executionContextMock.Object,
			MakeAllocateRequest(matchProperties: new Dictionary<string, object> { { "Region", "" } }));

		var body = JsonConvert.DeserializeObject<SessionRequest>(await capturedRequest!.Content!.ReadAsStringAsync());
		Assert.That(body!.Location, Is.EqualTo("europe"));
	}

	// ── Poll ──────────────────────────────────────────────────────────

	[Test]
	public async Task TestGameyePollReturnsAllocatedFromCachedData()
	{
		var poll = await CreateAllocator().Poll(_executionContextMock.Object,
			new PollRequest("match-1234", new Dictionary<string, object>
			{
				{ "sessionId", "test-session-id" },
				{ "host", "203.0.113.42" },
				{ "port", 49152 },
			}, DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
		Assert.That(poll.AssignmentData!.Type, Is.EqualTo(AssignmentType.IpPort));
		Assert.That(poll.AssignmentData.Ip, Is.EqualTo("203.0.113.42"));
		Assert.That(poll.AssignmentData.Port, Is.EqualTo(49152));
	}

	[Test]
	public async Task TestGameyePollFallsBackToApi()
	{
		SetupHttpResponse(new HttpResponseMessage
		{
			Content = new StringContent(JsonConvert.SerializeObject(new SessionResponse
			{
				Id = "test-session-id",
				Host = "203.0.113.42",
				Ports = [new PortMapping { Type = "udp", Container = 7777, Host = 49152 }],
				Status = "running",
			})),
		});

		var poll = await CreateAllocator().Poll(_executionContextMock.Object,
			new PollRequest("match-1234", new Dictionary<string, object>
			{
				{ "sessionId", "test-session-id" },
				{ "host", "" },
				{ "port", 0 },
			}, DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
		Assert.That(poll.AssignmentData!.Ip, Is.EqualTo("203.0.113.42"));
		Assert.That(poll.AssignmentData.Port, Is.EqualTo(49152));
	}

	[TestCase("running", PollStatus.Allocated)]
	[TestCase("created", PollStatus.Pending)]
	[TestCase("restarting", PollStatus.Pending)]
	[TestCase("exited", PollStatus.Error)]
	[TestCase("dead", PollStatus.Error)]
	[TestCase("unknown-state", PollStatus.Pending)]
	public async Task TestGameyePollMapsStatusCorrectly(string gameyeStatus, PollStatus expectedStatus)
	{
		SetupHttpResponse(new HttpResponseMessage
		{
			Content = new StringContent(JsonConvert.SerializeObject(new SessionResponse
			{
				Id = "test-session-id",
				Host = "203.0.113.42",
				Ports = [new PortMapping { Type = "udp", Container = 7777, Host = 49152 }],
				Status = gameyeStatus,
			})),
		});

		var poll = await CreateAllocator().Poll(_executionContextMock.Object,
			new PollRequest("match-1234", new Dictionary<string, object>
			{
				{ "sessionId", "test-session-id" },
				{ "host", "" },
				{ "port", 0 },
			}, DateTimeOffset.UtcNow));

		Assert.That(poll.Status, Is.EqualTo(expectedStatus));
	}

	// ── Error handling ────────────────────────────────────────────────

	[Test]
	public async Task TestGameyeAllocationError()
	{
		SetupHttpResponse(new HttpResponseMessage
		{
			StatusCode = System.Net.HttpStatusCode.NotFound,
			Content = new StringContent("Location not found"),
		});

		var allocation = await CreateAllocator().Allocate(_executionContextMock.Object, MakeAllocateRequest());

		Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
		Assert.That(allocation.Message, Is.Not.Null);
	}
}
