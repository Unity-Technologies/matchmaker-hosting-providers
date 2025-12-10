using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

public abstract class MatchmakerAllocator
{
    [CloudCodeFunction("Matchmaker_AllocateServer")]
    public abstract Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request);

    [CloudCodeFunction("Matchmaker_PollAllocation")]
    public abstract Task<PollResponse> Poll(IExecutionContext context, PollRequest request);
}

public class PollRequest
{
    public string MatchId { get; set; }
    public Dictionary<string, object> AllocationData { get; set; }
}

public class PollResponse
{
    public string Status { get; set; }
    public string Message { get; set; }
    public DateTimeOffset? AllocationCreatedTime { get; set; }
    /// <summary>
    /// REQUIRED when Status is "allocated". Valid values:
    /// "MultiplayAssignment", "IpPortAssignment", "CustomAssignment", "MatchIdAssignment"
    /// </summary>
    public string AssignmentType { get; set; }
    public string Ip { get; set; }
    public int? Port { get; set; }
    /// <summary>
    /// Optional custom data passed to clients (auth tokens, server metadata, etc.).
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; }
}

public class AllocateRequest
{
    public string MatchId { get; set; }
    public MatchmakingResults MatchmakingResults { get; set; }
}

public class MatchmakingResults
{
    /// <summary>
    /// Match properties containing player/team data and other match-specific configuration.
    /// Access player data like: request.MatchmakingResults.MatchProperties["teams"]
    /// </summary>
    public object MatchProperties { get; set; }
    public string GeneratorName { get; set; }
    public string QueueName { get; set; }
    public string PoolName { get; set; }
    public string EnvironmentId { get; set; }
    public string BackfillTicketId { get; set; }
    public string MatchId { get; set; }
    public string PoolId { get; set; }
}

public class AllocateResponse
{
    public string Status { get; set; }
    public Dictionary<string, object> AllocationData { get; set; }
    public string Message { get; set; }
}
