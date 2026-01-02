using AgonesAllocatorModule;
using AgonesAllocatorModule.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Microsoft.Kiota.Serialization.Json;
using Moq;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;

namespace AllocatorTests;

public class AgonesAllocatorTests
{
    private readonly Mock<ILogger<AgonesAllocator>> _loggerMock = new();
    private readonly Mock<IRequestAdapter> _requestAdapterMock = new();
    
    private readonly Mock<IExecutionContext> _executionContextMock = new();
    
    private readonly AgonesAllocator _allocator;

    public AgonesAllocatorTests()
    {
        _allocator = new AgonesAllocator(_requestAdapterMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task TestAgonesCanAllocate()
    {
        _requestAdapterMock.Reset();
        _requestAdapterMock.Setup(s => s.SerializationWriterFactory.GetSerializationWriter("application/json"))
            .Returns(new JsonSerializationWriter());
        
        _requestAdapterMock.Setup(r => r.SendAsync(It.IsAny<RequestInformation>(),
                It.IsAny<ParsableFactory<AllocationAllocationResponse>>(), null, CancellationToken.None))
            .ReturnsAsync(new AllocationAllocationResponse()
            {
                Ports = new List<AllocationResponseGameServerStatusPort>
                {
                    new()
                    {
                        Port = 1234,
                    }
                },
                Addresses = new List<AllocationResponseGameServerStatusAddress>
                {
                    new()
                    {
                        Address = "127.0.0.1",
                    }
                }
            });
        
        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new())));
        
        Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
        Assert.That(allocation.Message, Is.Null);
        Assert.That(allocation.AllocationData, Is.Not.Null);
        Assert.That(allocation.AllocationData["ip"], Is.EqualTo("127.0.0.1"));
        Assert.That(allocation.AllocationData["port"], Is.EqualTo(1234));
    }
    
    [Test]
    public async Task TestAgonesCanPoll()
    {
        var poll = await _allocator.Poll(_executionContextMock.Object, new PollRequest("1234",
            new Dictionary<string, object>
            {
                { "ip", "127.0.0.1" },
                { "port", 1234 },
            }, DateTimeOffset.UtcNow));
        
        Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
        Assert.That(poll.Message, Is.Null);
        Assert.That(poll.AssignmentData, Is.Not.Null);
        Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
        Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
        Assert.That(poll.AssignmentData.Port, Is.EqualTo(1234));
    }
}