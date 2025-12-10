using System.Collections.Generic;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

public class MatchmakingResults
{
    public Dictionary<string, object> MatchProperties { get; set; }

    public string GeneratorName { get; set; }

    public string QueueName { get; set; }

    public string PoolName { get; set; }

    public string EnvironmentId { get; set; }

    public string BackfillTicketId { get; set; }

    public string MatchId { get; set; }

    public string PoolId { get; set; }
}
