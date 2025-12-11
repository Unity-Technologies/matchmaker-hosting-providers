namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// The matchmaking results containing match properties, queue/pool info, and other match metadata.
/// This data should be used by the Cloud Code function to configure the allocated server.
/// Player and team information is available in MatchProperties.
/// </summary>
/// <param name="BackfillTicketId">
/// Backfill ticket if any associated with the match.
/// </param>
/// <param name="PoolId">
/// Identifier associated with the matchmaker pool.
/// </param>
/// <param name="PoolName">
/// Matchmaker pool name associated with the match.
/// </param>
/// <param name="QueueName">
/// Matchmaker queue associated with the match.
/// </param>
/// <param name="MatchProperties">
/// Custom properties defined from the client with information about the match.
/// </param>
public record MatchmakingResults(
    string? BackfillTicketId,
    string PoolId,
    string PoolName,
    string QueueName,
    Dictionary<string, object> MatchProperties
);