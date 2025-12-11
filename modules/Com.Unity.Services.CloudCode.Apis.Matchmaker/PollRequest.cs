namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Request payload for the Cloud Code poll function.
/// </summary>
/// <param name="MatchId">
/// The match identifier.
/// </param>
/// <param name="AllocationData">
/// The allocation data received from the initial allocate call.
/// This is the opaque data returned by the allocate function for tracking purposes.
/// </param>
/// <param name="AllocationCreatedTime">
/// The time when the allocation was created (after allocate function succeeded).
/// Developers can use this to implement timeout logic in their poll function.
/// </param>
public record PollRequest(
    string MatchId,
    Dictionary<string, object> AllocationData,
    DateTimeOffset AllocationCreatedTime
);
