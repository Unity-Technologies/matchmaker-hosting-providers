namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Status values for Cloud Code poll responses.
/// These values are returned by the Cloud Code poll function to indicate the current state of the allocation.
/// </summary>
public enum PollStatus
{
    /// <summary>
    /// Allocation is still in progress. The matchmaker will continue polling.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Server allocated successfully. Connection data is available.
    /// </summary>
    Allocated = 1,

    /// <summary>
    /// Allocation failed. Check the Message property for details.
    /// </summary>
    Error = 2,
}
