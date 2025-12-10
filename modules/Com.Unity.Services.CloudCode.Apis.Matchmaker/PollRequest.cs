using System;
using System.Collections.Generic;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Request payload for the Cloud Code poll function.
/// </summary>
public class PollRequest
{
    /// <summary>
    /// Gets or sets the match ID.
    /// </summary>
    public string MatchId { get; set; }

    /// <summary>
    /// Gets or sets the allocation data received from the initial allocate call.
    /// This is the opaque data returned by the allocate function for tracking purposes.
    /// </summary>
    public Dictionary<string, object> AllocationData { get; set; }

    /// <summary>
    /// Gets or sets the time when the allocation was created (after allocate function succeeded).
    /// Developers can use this to implement timeout logic in their poll function.
    /// </summary>
    public DateTimeOffset AllocationCreatedTime { get; set; }
}
