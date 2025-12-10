namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Request payload for the Cloud Code allocate function.
/// Mirrors the data that Multiplay receives via payloadAllocation.
/// </summary>
public class AllocateRequest
{
    /// <summary>
    /// Gets or sets the match ID.
    /// </summary>
    public string MatchId { get; set; }

    /// <summary>
    /// Gets or sets the matchmaking results containing match properties, queue/pool info, and other match metadata.
    /// This data should be used by the Cloud Code function to configure the allocated server.
    /// Player and team information is available in MatchProperties.
    /// </summary>
    public MatchmakingResults MatchmakingResults { get; set; }
}
