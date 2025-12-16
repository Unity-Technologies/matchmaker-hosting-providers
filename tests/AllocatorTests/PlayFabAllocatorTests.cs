using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;
using PlayFab;
using PlayFab.AuthenticationModels;
using PlayFab.MultiplayerModels;
using PlayFabAllocatorModule;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using EntityKey = PlayFab.AuthenticationModels.EntityKey;
using Secret = Unity.Services.CloudCode.Apis.Secret;

namespace AllocatorTests;

public class PlayFabAllocatorTests
{
    readonly Mock<IPlayFabFactory> _playFabFactoryMock = new();
    readonly Mock<ISecretClient> _secretClientMock = new();
    readonly Mock<IGameApiClient> _gameClientMock = new();
    readonly FakeLogger<PlayFabAllocator> _fakeLogger = new();
    readonly Mock<IPlayFabAuthenticationApi> _authenticationApiMock = new();
    readonly Mock<IPlayFabMultiplayerInstanceAPI> _multiplayerInstanceApiMock = new();
    readonly Mock<IExecutionContext> _executionContextMock = new();

    PlayFabAllocator _allocator;

    [SetUp]
    public void SetUp()
    {
        _gameClientMock.SetupGet(g => g.SecretManager).Returns(_secretClientMock.Object);
        _secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
            .ReturnsAsync(new Secret("secret"));
        _playFabFactoryMock.Setup(f => f.CreateMultiplayerInstanceApi(It.IsAny<PlayFabApiSettings>(), It.IsAny<PlayFabAuthenticationContext>()))
            .Returns(_multiplayerInstanceApiMock.Object);
        _allocator = new PlayFabAllocator(_gameClientMock.Object, _playFabFactoryMock.Object, _authenticationApiMock.Object, _fakeLogger);
    }

    [Test]
    public async Task WillLogAndReturnAllocationErrorWhenSecretIsNotFound()
    {
        _secretClientMock.Setup(s => s.GetSecret(_executionContextMock.Object, It.IsAny<string>()))
            .ThrowsAsync(new Exception("Secret not found."));

         var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_fakeLogger.Collector.LatestRecord.Message, Is.EqualTo("An error occured when retrieving secrets for key 'DEVELOPER_SECRET_KEY'."));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
        }
    }

    [Test]
    public async Task WillLogAndReturnAllocationErrorWhenAuthenticationFails()
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>())).Throws<Exception>();

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_fakeLogger.Collector.LatestRecord.Message, Is.EqualTo("An error occured when retrieving the entity token."));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
        }
    }

    static IEnumerable<TestCaseData> InvalidAuthenticationResultTestCases()
    {
        yield return new TestCaseData(null, "The result is null");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Error = new PlayFabError { ErrorMessage = "Authentication failed" }
            },
            "An error occured when calling");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            },
            "Token is malformed");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "", Type = "entityType" }
                }
            },
            "Token is malformed");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "" }
                }
            },
            "Token is malformed");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "", Type = "" }
                }
            },
            "Token is malformed");
        yield return new TestCaseData(
            new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "",
                    Entity = new EntityKey { Id = "", Type = "" }
                }
            },
            "Token is malformed");
    }

    [TestCaseSource(nameof(InvalidAuthenticationResultTestCases))]
    public async Task WillLogAndReturnAllocationErrorWhenAuthenticationResultIsInvalid(
        PlayFabResult<GetEntityTokenResponse> tokenResponse,
        string expectedLogMessagePart)
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(tokenResponse);

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_fakeLogger.Collector.LatestRecord.Message, Does.Contain(expectedLogMessagePart));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
        }
    }

    static IEnumerable<TestCaseData> InvalidAllocationResultTestCases()
    {
        yield return new TestCaseData(null, "The result is null");
        yield return new TestCaseData(
            new PlayFabResult<RequestMultiplayerServerResponse>
            {
                Error = new PlayFabError { ErrorMessage = "Allocation failed" }
            },
            "An error occured when calling");
    }

    [TestCaseSource(nameof(InvalidAllocationResultTestCases))]
    public async Task WillLogAndReturnAllocationErrorWhenAllocationResultIsInvalid(
        PlayFabResult<RequestMultiplayerServerResponse> allocationResult,
        string expectedLogMessagePart)
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            });

        _multiplayerInstanceApiMock.Setup(m => m.RequestMultiplayerServerAsync(It.IsAny<RequestMultiplayerServerRequest>()))
            .ReturnsAsync(allocationResult);

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_fakeLogger.Collector.LatestRecord.Message, Does.Contain(expectedLogMessagePart));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
        }
    }

    [Test]
    public async Task WillAllocateToDefaultRegionWhenRegionIsMissing()
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            });

        _multiplayerInstanceApiMock.Setup(m => m.RequestMultiplayerServerAsync(It.IsAny<RequestMultiplayerServerRequest>()))
            .ReturnsAsync(new PlayFabResult<RequestMultiplayerServerResponse>
            {
                Result = new RequestMultiplayerServerResponse
                {
                    SessionId = "sessionId"
                }
            });

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>())));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
            Assert.That(allocation.Message, Is.Null);
            Assert.That(allocation.AllocationData, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.AllocationData["playfabRegion"], Is.EqualTo("EastUs"));
            Assert.That(allocation.AllocationData["sessionId"], Is.EqualTo("sessionId"));
            Assert.That(allocation.AllocationData["matchId"], Is.EqualTo("1234"));
        }
    }

    [Test]
    public async Task WillAllocateToSpecificRegion()
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            });

        _multiplayerInstanceApiMock.Setup(m => m.RequestMultiplayerServerAsync(It.IsAny<RequestMultiplayerServerRequest>()))
            .ReturnsAsync(new PlayFabResult<RequestMultiplayerServerResponse>
            {
                Result = new RequestMultiplayerServerResponse
                {
                    SessionId = "sessionId"
                }
            });

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>
            {
                { "region", "customRegion" }
            })));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
            Assert.That(allocation.Message, Is.Null);
            Assert.That(allocation.AllocationData, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.AllocationData["playfabRegion"], Is.EqualTo("customRegion"));
            Assert.That(allocation.AllocationData["sessionId"], Is.EqualTo("sessionId"));
            Assert.That(allocation.AllocationData["matchId"], Is.EqualTo("1234"));
        }
    }

    [Test]
    public async Task WillLogAndReturnAllocationErrorWhenRegionIsEmpty()
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            });

        _multiplayerInstanceApiMock.Setup(m => m.RequestMultiplayerServerAsync(It.IsAny<RequestMultiplayerServerRequest>()))
            .ReturnsAsync(new PlayFabResult<RequestMultiplayerServerResponse>
            {
                Result = new RequestMultiplayerServerResponse
                {
                    SessionId = "sessionId"
                }
            });

        var allocation = await _allocator.Allocate(_executionContextMock.Object, new AllocateRequest("1234",
            new MatchmakingResults(null, "poolId", "poolName", "queueName", new Dictionary<string, object>
            {
                { "region", string.Empty }
            })));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(_fakeLogger.Collector.LatestRecord.Message, Is.EqualTo("An error occured when retrieving the region in matchmaking properties. The region field must be non-empty."));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(allocation.Message, Is.Not.Null);
            Assert.That(allocation.AllocationData, Is.Null);
        }
    }

    [Test]
    public async Task TestThatPlayFabCanPoll()
    {
        _authenticationApiMock.Setup(a => a.GetEntityTokenAsync(It.IsAny<PlayFabApiSettings>()))
            .ReturnsAsync(new PlayFabResult<GetEntityTokenResponse>
            {
                Result = new GetEntityTokenResponse
                {
                    EntityToken = "token",
                    Entity = new EntityKey { Id = "entityId", Type = "entityType" }
                }
            });

        _multiplayerInstanceApiMock.Setup(m => m.GetMultiplayerServerDetailsAsync(It.Is<GetMultiplayerServerDetailsRequest>(r => r.SessionId == "sessionId")))
            .ReturnsAsync(new PlayFabResult<GetMultiplayerServerDetailsResponse>
            {
                Result = new GetMultiplayerServerDetailsResponse
                {
                    State = "Active",
                    IPV4Address = "127.0.0.1",
                    Ports = [new Port { Num = 1234 }]
                }
            });

        var poll = await _allocator.Poll(_executionContextMock.Object, new PollRequest("1234",
            new Dictionary<string, object>
            {
                { "sessionId", "sessionId" },
                { "playfabRegion", "EastUs" }
            }, DateTimeOffset.UtcNow));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
            Assert.That(poll.Message, Is.Null);
            Assert.That(poll.AssignmentData, Is.Not.Null);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(poll.AssignmentData.Type, Is.EqualTo(AssignmentType.IpPort));
            Assert.That(poll.AssignmentData.Ip, Is.EqualTo("127.0.0.1"));
            Assert.That(poll.AssignmentData.Port, Is.EqualTo(1234));
        }
    }
}
