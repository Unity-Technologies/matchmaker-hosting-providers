namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Request payload for the Cloud Code allocate function.
/// Mirrors the data that Multiplay receives via payloadAllocation.
/// </summary>
/// <param name="MatchId">
/// The match identifier.
/// </param>
/// <param name="MatchmakingResults">
/// The matchmaking results containing match properties, queue/pool info, and other match metadata.
/// This data should be used by the Cloud Code function to configure the allocated server.
/// Player and team information is available in MatchProperties.
/// </param>
public record AllocateRequest(
    string MatchId, 
    MatchmakingResults MatchmakingResults
);
