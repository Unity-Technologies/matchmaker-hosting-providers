using System.Net;
using AgonesAllocatorModule;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;

namespace AllocatorTests;

public class AgonesAllocatorTests
{
    private readonly Mock<ILogger<AgonesAllocator>> _loggerMock = new();
    private readonly Mock<IExecutionContext> _executionContextMock = new();

    [Test]
    public async Task TestAgonesCanAllocate()
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    "{\"address\":\"127.0.0.1\",\"ports\":[{\"port\":1234}],\"gameServerName\":\"test-server\"}")
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var allocator = new AgonesAllocator(_loggerMock.Object, httpClient);

        var allocation = await allocator.Allocate(_executionContextMock.Object,
            new AllocateRequest("1234",
                new MatchmakingResults(null, "matchId", "poolId", "poolName", "queueName", new())));

        Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
        Assert.That(allocation.AllocationData["ip"], Is.EqualTo("127.0.0.1"));
        Assert.That(allocation.AllocationData["port"], Is.EqualTo(1234));
    }

    [Test]
    public async Task TestAgonesCanPoll()
    {
        var allocator = new AgonesAllocator(_loggerMock.Object);

        var poll = await allocator.Poll(_executionContextMock.Object,
            new PollRequest("1234",
                new Dictionary<string, object>
                {
                    { "ip", "127.0.0.1" },
                    { "port", 1234 },
                }, DateTimeOffset.UtcNow));

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
        Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
        Assert.That(poll.AssignmentData.Port, Is.EqualTo(1234));
    }
}