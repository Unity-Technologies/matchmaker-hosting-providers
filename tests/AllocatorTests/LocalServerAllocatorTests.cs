using System.Net;
using System.Text.Json;
using LocalServerAllocatorModule;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Apis.Matchmaker;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Lobby.Api;
using Unity.Services.Lobby.Model;

namespace AllocatorTests;

public class LocalServerAllocatorTests
{
    const string MatchId = "match-1";
    const string ServiceToken = "service-token";
    const string AccessToken = "access-token";
    const string ControlSessionId = "local-dev-a";
    const string SessionNetworkKey = "_session_network";
    const string PlayersOnDevA =
        """{"players":[{"id":"p1","customData":{"localServerId":"dev-a"}},{"id":"p2","customData":{"localServerId":"dev-a"}}]}""";

    readonly FakeLogger<LocalServerAllocator> _fakeLogger = new();
    readonly Mock<IGameApiClient> _gameApiClientMock = new();
    readonly Mock<ILobbyApi> _lobbyApiMock = new();
    readonly Mock<IExecutionContext> _executionContextMock = new();

    LocalServerAllocator _allocator;

    [SetUp]
    public void SetUp()
    {
        _lobbyApiMock.Reset();
        _fakeLogger.Collector.Clear();
        _executionContextMock.SetupGet(c => c.ServiceToken).Returns(ServiceToken);
        _executionContextMock.SetupGet(c => c.AccessToken).Returns(AccessToken);
        _gameApiClientMock.SetupGet(g => g.Lobby).Returns(_lobbyApiMock.Object);
        _allocator = new LocalServerAllocator(_gameApiClientMock.Object, _fakeLogger);
    }

    [Test]
    public async Task AllocateReturnsErrorWhenPlayersDoNotShareALocalServerId()
    {
        var allocation = await _allocator.Allocate(_executionContextMock.Object, AllocateRequestFor(
            """{"players":[{"id":"p1","customData":{"localServerId":"dev-a"}},{"id":"p2","customData":{}}]}"""));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(allocation.Message, Does.Contain(LocalServerIds.PlayerPropertyKey));
            Assert.That(_lobbyApiMock.Invocations, Is.Empty);
        }
    }

    [Test]
    public void AllocatorPlayerIdIsAValidLobbyPlayerId()
    {
        Assert.That(LocalServerAllocator.AllocatorPlayerId, Does.Match("^[A-Za-z0-9]{28}$"),
            "Lobby rejects impersonated player ids that are not 28-character alphanumeric strings (HTTP 400).");
    }

    [Test]
    public async Task AllocateJoinsTheControlSessionAsTheAllocatorPlayerWithThePendingMatchId()
    {
        SetupJoin().ReturnsAsync(new ApiResponse<Lobby>());

        var allocation = await _allocator.Allocate(_executionContextMock.Object, AllocateRequestFor(PlayersOnDevA));

        _lobbyApiMock.Verify(l => l.JoinLobbyByIdAsync(_executionContextMock.Object, ServiceToken, ControlSessionId,
            It.IsAny<string>(), LocalServerAllocator.AllocatorPlayerId,
            It.Is<Player>(player => player.Id == LocalServerAllocator.AllocatorPlayerId &&
                CarriesPendingMatchId(player.Data)),
            It.IsAny<CancellationToken>()), Times.Once);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
            Assert.That(allocation.AllocationData!["matchId"], Is.EqualTo(MatchId));
        }
    }

    [Test]
    public async Task AllocateUpdatesTheAllocatorPlayerWhenItIsAlreadyInTheControlSession()
    {
        SetupJoin().ThrowsAsync(ApiExceptionWith(HttpStatusCode.Conflict));
        _lobbyApiMock.Setup(l => l.UpdatePlayerAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<PlayerUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<Lobby>());

        var allocation = await _allocator.Allocate(_executionContextMock.Object, AllocateRequestFor(PlayersOnDevA));

        _lobbyApiMock.Verify(l => l.UpdatePlayerAsync(_executionContextMock.Object, ServiceToken, ControlSessionId,
            LocalServerAllocator.AllocatorPlayerId, It.IsAny<string>(), LocalServerAllocator.AllocatorPlayerId,
            It.Is<PlayerUpdateRequest>(request => CarriesPendingMatchId(request.Data)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Created));
    }

    [Test]
    public async Task AllocateReportsThatNoLocalServerIsRunningWhenTheControlSessionDoesNotExist()
    {
        SetupJoin().ThrowsAsync(ApiExceptionWith(HttpStatusCode.NotFound));

        var allocation = await _allocator.Allocate(_executionContextMock.Object, AllocateRequestFor(PlayersOnDevA));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
            Assert.That(allocation.Message, Is.EqualTo(
                $"No local server is running for 'dev-a' (control session '{ControlSessionId}' not found). Start the server first."));
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Warning));
        }
    }

    [Test]
    public async Task AllocateLogsAndReturnsErrorWhenSignallingTheControlSessionFails()
    {
        SetupJoin().ThrowsAsync(new Exception("Lobby unavailable."));

        var allocation = await _allocator.Allocate(_executionContextMock.Object, AllocateRequestFor(PlayersOnDevA));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(allocation.Status, Is.EqualTo(AllocateStatus.Error));
        }
    }

    [Test]
    public async Task PollIsPendingWhileTheMatchSessionDoesNotExist()
    {
        SetupGetLobby().ThrowsAsync(ApiExceptionWith(HttpStatusCode.NotFound));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Pending));
    }

    [Test]
    public async Task PollIsPendingWhileTheMatchSessionHasNoNetwork()
    {
        SetupGetLobby().ReturnsAsync(LobbyResponse(new Dictionary<string, DataObject>()));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Pending));
    }

    [Test]
    public async Task PollReturnsTheServerAddressForADirectNetwork()
    {
        SetupGetLobby().ReturnsAsync(LobbyWithNetwork("""{"Network":0,"Ip":"203.0.113.5","Port":7777}"""));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
            Assert.That(poll.AssignmentData!.Type, Is.EqualTo(AssignmentType.IpPort));
            Assert.That(poll.AssignmentData.Ip, Is.EqualTo("203.0.113.5"));
            Assert.That(poll.AssignmentData.Port, Is.EqualTo(7777));
        }
    }

    [Test]
    public async Task PollReturnsACustomAssignmentForARelayNetwork()
    {
        SetupGetLobby().ReturnsAsync(LobbyWithNetwork("""{"Network":1,"RelayJoinCode":"ABC123","RelayRegion":null}"""));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Allocated));
            Assert.That(poll.AssignmentData!.Type, Is.EqualTo(AssignmentType.Custom));
        }
    }

    [TestCase("""{"Network":0,"Ip":"203.0.113.5","Port":0}""", TestName = "PollIsPendingForADirectNetworkWithoutAPort")]
    [TestCase("""{"Network":0,"Ip":"","Port":7777}""", TestName = "PollIsPendingForADirectNetworkWithoutAnIp")]
    [TestCase("""{"Network":1,"RelayJoinCode":""}""", TestName = "PollIsPendingForARelayNetworkWithoutAJoinCode")]
    public async Task PollIsPendingForAnIncompleteNetwork(string sessionNetwork)
    {
        SetupGetLobby().ReturnsAsync(LobbyWithNetwork(sessionNetwork));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        Assert.That(poll.Status, Is.EqualTo(PollStatus.Pending));
    }

    [Test]
    public async Task PollLogsAndReturnsErrorWhenReadingTheMatchSessionFails()
    {
        SetupGetLobby().ThrowsAsync(new Exception("Lobby unavailable."));

        var poll = await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_fakeLogger.Collector.LatestRecord.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(poll.Status, Is.EqualTo(PollStatus.Error));
        }
    }

    [Test]
    public async Task PollReadsTheMatchSessionWithTheServiceToken()
    {
        SetupGetLobby().ReturnsAsync(LobbyWithNetwork("""{"Network":0,"Ip":"203.0.113.5","Port":7777}"""));

        await _allocator.Poll(_executionContextMock.Object, PollRequestForMatch());

        _lobbyApiMock.Verify(l => l.GetLobbyAsync(_executionContextMock.Object, ServiceToken, MatchId, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _lobbyApiMock.Verify(l => l.GetLobbyAsync(It.IsAny<IExecutionContext>(), AccessToken, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    Moq.Language.Flow.ISetup<ILobbyApi, Task<ApiResponse<Lobby>>> SetupJoin() =>
        _lobbyApiMock.Setup(l => l.JoinLobbyByIdAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Player>(),
            It.IsAny<CancellationToken>()));

    Moq.Language.Flow.ISetup<ILobbyApi, Task<ApiResponse<Lobby>>> SetupGetLobby() =>
        _lobbyApiMock.Setup(l => l.GetLobbyAsync(It.IsAny<IExecutionContext>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()));

    static bool CarriesPendingMatchId(Dictionary<string, PlayerDataObject>? data) =>
        data is not null &&
        data.TryGetValue(LocalServerAllocator.PendingMatchIdKey, out var pendingMatchId) &&
        pendingMatchId.Value == MatchId &&
        pendingMatchId.Visibility == PlayerDataObject.VisibilityEnum.Member;

    static AllocateRequest AllocateRequestFor(string matchProperties) =>
        new(MatchId, new MatchmakingResults(null, MatchId, "poolId", "poolName", "queueName",
            JsonSerializer.Deserialize<Dictionary<string, object>>(matchProperties)!));

    static PollRequest PollRequestForMatch() =>
        new(MatchId, new Dictionary<string, object> { ["matchId"] = MatchId }, DateTimeOffset.UtcNow);

    static ApiException ApiExceptionWith(HttpStatusCode statusCode)
    {
        var response = new Mock<IApiResponse>();
        response.SetupGet(r => r.StatusCode).Returns(statusCode);
        return new ApiException(ApiExceptionType.Http, statusCode.ToString(), response.Object);
    }

    static ApiResponse<Lobby> LobbyWithNetwork(string sessionNetwork) =>
        LobbyResponse(new Dictionary<string, DataObject> { [SessionNetworkKey] = new(sessionNetwork) });

    static ApiResponse<Lobby> LobbyResponse(Dictionary<string, DataObject> data)
    {
        // ApiResponse<T>.Data has an internal setter; reflection is the only way to build a response in a test.
        var response = new ApiResponse<Lobby>();
        typeof(ApiResponse<Lobby>).GetProperty(nameof(ApiResponse<Lobby>.Data))!.SetValue(response, new Lobby(data: data));
        return response;
    }
}
