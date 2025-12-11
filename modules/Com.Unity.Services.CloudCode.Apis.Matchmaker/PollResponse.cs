

using Newtonsoft.Json;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Response payload from the Cloud Code poll function.
/// </summary>
public class PollResponse
{
    public PollResponse(PollStatus status)
    {
        Status = status;
    }
    
    /// <summary>
    /// The current status of the allocation.
    /// </summary>
    [JsonProperty("status")]
    public PollStatus Status { get; }

    /// <summary>
    /// A human-readable message describing the current state.
    /// Provides additional context for any status, especially useful for errors.
    /// </summary>
    [JsonProperty("message", NullValueHandling=NullValueHandling.Ignore)]
    public string? Message { get; init; }

    /// <summary>
    /// The assignment data containing connection details.
    /// Required when Status is Allocated.
    /// </summary>
    [JsonProperty("assignmentData", NullValueHandling=NullValueHandling.Ignore)]
    public AssignmentData? AssignmentData { get; init; }
}

/// <summary>
/// Base class for Cloud Code assignment data.
/// Use the appropriate derived class based on AssignmentType.
/// </summary>
public abstract class AssignmentData
{
    [JsonProperty("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Assignment data for IpPortAssignment type.
/// Use this for modern third-party provider integrations.
/// </summary>
public class IpPortAssignmentData : AssignmentData
{
    public override string Type => "ipPort";

    /// <summary>
    /// The server IP address.
    /// </summary>
    [JsonProperty("ip", NullValueHandling=NullValueHandling.Ignore)]
    public string? Ip { get; init; }

    /// <summary>
    /// The server port.
    /// </summary>
    [JsonProperty("port", NullValueHandling=NullValueHandling.Ignore)]
    public int? Port { get; init; }

    /// <summary>
    /// Custom data to pass through to clients.
    /// Use this for auth tokens, session metadata, provider-specific extras, etc.
    /// </summary>
    [JsonProperty("customData", NullValueHandling=NullValueHandling.Ignore)]
    public Dictionary<string, object>? CustomData { get; init; }
}

/// <summary>
/// Assignment data for CustomAssignment type.
/// Use this when connection is entirely custom (no IP/port).
/// </summary>
public class CustomAssignmentData : AssignmentData
{
    public override string Type => "custom";

    /// <summary>
    /// Custom data to pass through to clients.
    /// This is the primary payload for custom connections.
    /// </summary>
    [JsonProperty("customData", NullValueHandling=NullValueHandling.Ignore)]
    public Dictionary<string, object>? CustomData { get; init; }
}
