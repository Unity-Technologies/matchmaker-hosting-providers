using Newtonsoft.Json;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Response payload from the Cloud Code allocate function.
/// </summary>
public class AllocateResponse
{
    public AllocateResponse(AllocateStatus status)
    {
        Status = status;
    }
    
    /// <summary>
    /// The status of the allocation request.
    /// </summary>
    [JsonProperty("status")]
    public AllocateStatus Status { get; }

    /// <summary>
    /// A human-readable message describing the result.
    /// Provides additional context for any status, especially useful for errors.
    /// </summary>
    [JsonProperty("message", NullValueHandling=NullValueHandling.Ignore)]
    public string? Message { get; init; }

    /// <summary>
    /// Allocation tracking data returned by the Cloud Code function.
    /// This data will be passed back in subsequent poll requests.
    /// Developers should include any data needed to poll the allocation status (e.g., allocation ID, provider-specific tracking data).
    /// </summary>
    [JsonProperty("allocationData", NullValueHandling=NullValueHandling.Ignore)]
    public Dictionary<string, object>? AllocationData { get; init; }
}
