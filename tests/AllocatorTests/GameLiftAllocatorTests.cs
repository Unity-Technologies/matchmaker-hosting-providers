using Amazon.GameLift;
using Amazon.GameLift.Model;
using GameLiftAllocatorModule;
using NUnit.Framework;
using Moq;
using Unity.Services.CloudCode.Apis;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;

namespace AllocatorTests;

public class GameLiftAllocatorTests
{
    private readonly Mock<IGameLiftFactory> _gameLiftFactoryMock = new();
    private readonly Mock<ISecretClient> _secretClientMock = new();
    private readonly Mock<IGameApiClient> _gameClientMock = new();
    private readonly Mock<ILogger<GameLiftAllocator>> _loggerMock = new();
    private readonly Mock<IAmazonGameLift> _gameLiftMock = new();

    private readonly Mock<IExecutionContext> _executionContextMock = new();

    private readonly GameLiftAllocator _allocator;

    public GameLiftAllocatorTests()
    {
        _gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
        _secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
            .ReturnsAsync(new Secret("secret"));
        _gameLiftFactoryMock.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_gameLiftMock.Object);
        _allocator = new GameLiftAllocator(_gameClientMock.Object, _gameLiftFactoryMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task TestThatGameLiftCanAllocate()
    {
        _gameLiftMock.Reset();
        _gameLiftMock.Setup(g => g.StartGameSessionPlacementAsync(It.IsAny<StartGameSessionPlacementRequest>(), CancellationToken.None))
            .ReturnsAsync(new StartGameSessionPlacementResponse
            {
                GameSessionPlacement = new GameSessionPlacement
                {
                    PlacementId = "placementId",
                }
            });
        
        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new())));

        Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
        Assert.That(allocation.Message, Is.Null);
        Assert.That(allocation.AllocationData, Is.Not.Null);
        Assert.That(allocation.AllocationData["awsRegion"], Is.EqualTo("eu-west-2"));
        Assert.That(allocation.AllocationData["placementId"], Is.EqualTo("placementId"));
    }
    
    [Test]
    public async Task TestThatGameLiftCanAllocateToRegions()
    {
        _gameLiftMock.Reset();
        _gameLiftMock.Setup(g => g.StartGameSessionPlacementAsync(It.IsAny<StartGameSessionPlacementRequest>(), CancellationToken.None))
            .ReturnsAsync(new StartGameSessionPlacementResponse
            {
                GameSessionPlacement = new GameSessionPlacement
                {
                    PlacementId = "placementId",
                }
            });
        
        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new
            Dictionary<string, object>{
                {"region", "customRegion"},
            })));
        
        Assert.That(allocation.AllocationData, Is.Not.Null);
        Assert.That(allocation.AllocationData["awsRegion"], Is.EqualTo("customRegion"));
    }
    
    [Test]
    public async Task TestThatGameLiftCanPoll()
    {
        _gameLiftMock.Reset();
        _gameLiftMock.Setup(g => g.DescribeGameSessionPlacementAsync(It.IsAny<DescribeGameSessionPlacementRequest>(), CancellationToken.None))
            .ReturnsAsync(new DescribeGameSessionPlacementResponse()
            {
                GameSessionPlacement = new GameSessionPlacement
                {
                    Status = new GameSessionPlacementState("FULFILLED"),
                    IpAddress = "127.0.0.1",
                    Port = 1234,
                }
            });
        
        var poll = await _allocator.Poll(_executionContextMock.Object, new PollRequest("1234",
            new Dictionary<string, object>
            {
                { "placementId", "placementId" },
                { "awsRegion", "awsRegion" },
            }, DateTimeOffset.UtcNow));

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
        Assert.That(poll.Message, Is.Null);
        Assert.That(poll.AssignmentData, Is.Not.Null);
        Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
        Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
        Assert.That(poll.AssignmentData.Port, Is.EqualTo(1234));
    }
}
