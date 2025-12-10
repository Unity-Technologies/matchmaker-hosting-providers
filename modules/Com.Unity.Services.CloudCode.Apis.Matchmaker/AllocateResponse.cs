using System.Collections.Generic;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Response payload from the Cloud Code allocate function.
/// </summary>
public class AllocateResponse
{
    /// <summary>
    /// Gets or sets the status of the allocation request.
    /// </summary>
    public AllocateStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the result.
    /// Provides additional context for any status, especially useful for errors.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets allocation tracking data returned by the Cloud Code function.
    /// This data will be passed back in subsequent poll requests.
    /// Developers should include any data needed to poll the allocation status (e.g., allocation ID, provider-specific tracking data).
    /// </summary>
    public Dictionary<string, object> AllocationData { get; set; }
}
