using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using RocketScienceAllocatorModule;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;

namespace AllocatorTests;

public class RocketScienceAllocatorTests
{
    private readonly Mock<ISecretClient> _secretClientMock = new();
    private readonly Mock<IGameApiClient> _gameClientMock = new();
    private readonly Mock<ILogger<RocketScienceAllocator>> _loggerMock = new();
    private readonly Mock<IRocketScienceHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();

    private readonly Mock<IExecutionContext> _executionContextMock = new();

    private readonly RocketScienceAllocator _allocator;

    public RocketScienceAllocatorTests()
    {
        _gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
        _secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
            .ReturnsAsync(new Secret("secret"));
        _httpClientFactoryMock.Setup(f => f.Create(It.IsAny<string>()))
            .Returns(() => new HttpClient(_httpMessageHandlerMock.Object));
        _allocator = new RocketScienceAllocator(_gameClientMock.Object, _httpClientFactoryMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task TestRocketScienceCanAllocate()
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
                Content = new StringContent("{'allocationId': 'allocationId'}")
            });

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

        Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
        Assert.That(allocation.Message, Is.Null);
        Assert.That(allocation.AllocationData, Is.Not.Null);
        Assert.That(allocation.AllocationData["allocationId"], Is.EqualTo("allocationId"));
    }

    [Test]
    public async Task TestRocketScienceAllocatorUsesDefaultsRegionWhenEmptyString()
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
                Content = new StringContent("{'allocationId': 'allocationId'}")
            });
        
        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new
            Dictionary<string, object>{
                {"Region", ""},
            })));
        
        Assert.That(allocation.AllocationData, Is.Not.Null);
        Assert.That(allocation.AllocationData["region"], Is.EqualTo("your_default_region"));
    }

    [Test]
    public async Task TestRocketScienceCanPoll()
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
                Content = new StringContent("{'allocationId': 'allocationId', 'fulfilled': 'true', 'readiness': false, 'ipv4': '127.0.0.1', 'gamePort': 1234}")
            });

        var poll = await _allocator.Poll(_executionContextMock.Object, new PollRequest("1234",
            new Dictionary<string, object>
            {
                { "allocationId", "allocationId" },
            }, DateTimeOffset.UtcNow));

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
        Assert.That(poll.Message, Is.Null);
        Assert.That(poll.AssignmentData, Is.Not.Null);
        Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
        Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
        Assert.That(poll.AssignmentData.Port, Is.EqualTo(1234));
    }

    // When RocketScienceProjectID and RocketScienceEnvironmentID are empty (the default), the
    // allocator must fall back to the project and environment IDs from the execution context so
    // that users who do not need cross-project allocation get the correct behaviour without any
    // extra configuration.
    [Test]
    public async Task TestAllocateUsesContextProjectAndEnvironmentIdWhenOverridesAreEmpty()
    {
        const string contextProjectId = "context-project-id";
        const string contextEnvironmentId = "context-environment-id";
        _executionContextMock.SetupGet(c => c.ProjectId).Returns(contextProjectId);
        _executionContextMock.SetupGet(c => c.EnvironmentId).Returns(contextEnvironmentId);

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock.Reset();
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage()
            {
                Content = new StringContent("{'allocationId': 'allocationId'}")
            });

        await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.RequestUri!.ToString(), Does.Contain($"/projects/{contextProjectId}/"));
        Assert.That(capturedRequest.RequestUri.ToString(), Does.Contain($"/environments/{contextEnvironmentId}/"));
    }

    [Test]
    public async Task TestPollUsesContextProjectAndEnvironmentIdWhenOverridesAreEmpty()
    {
        const string contextProjectId = "context-project-id";
        const string contextEnvironmentId = "context-environment-id";
        _executionContextMock.SetupGet(c => c.ProjectId).Returns(contextProjectId);
        _executionContextMock.SetupGet(c => c.EnvironmentId).Returns(contextEnvironmentId);

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock.Reset();
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage()
            {
                Content = new StringContent("{'allocationId': 'allocationId', 'fulfilled': 'true', 'readiness': false, 'ipv4': '127.0.0.1', 'gamePort': 1234}")
            });

        await _allocator.Poll(_executionContextMock.Object, new PollRequest("1234",
            new Dictionary<string, object>
            {
                { "allocationId", "allocationId" },
            }, DateTimeOffset.UtcNow));

        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.RequestUri!.ToString(), Does.Contain($"/projects/{contextProjectId}/"));
        Assert.That(capturedRequest.RequestUri.ToString(), Does.Contain($"/environments/{contextEnvironmentId}/"));
    }
}
