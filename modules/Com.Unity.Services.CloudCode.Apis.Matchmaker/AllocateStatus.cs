namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Status values for Cloud Code allocation responses.
/// These values are returned by the Cloud Code allocate function to indicate the result of the allocation request.
/// </summary>
public enum AllocateStatus
{
    /// <summary>
    /// Allocation job was created successfully. The matchmaker will begin polling for completion.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Allocation request failed. Check the Message property for details.
    /// </summary>
    Error = 1,
}
