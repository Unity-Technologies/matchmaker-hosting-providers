

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Response payload from the Cloud Code poll function.
/// </summary>
public class PollResponse
{
    /// <summary>
    /// Gets or sets the current status of the allocation.
    /// </summary>
    public PollStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a human-readable message describing the current state.
    /// Provides additional context for any status, especially useful for errors.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the assignment data containing connection details.
    /// Required when Status is Allocated.
    /// </summary>
    public AssignmentData AssignmentData { get; set; }
}

/// <summary>
/// Base class for Cloud Code assignment data.
/// Use the appropriate derived class based on AssignmentType.
/// </summary>
public abstract class AssignmentData
{
    public abstract string Type { get; }
}

/// <summary>
/// Assignment data for IpPortAssignment type.
/// Use this for modern third-party provider integrations.
/// </summary>
public class IpPortAssignmentData : AssignmentData
{
    public override string Type => "IpPort";

    /// <summary>
    /// Gets or sets the server IP address.
    /// </summary>
    public string Ip { get; set; }

    /// <summary>
    /// Gets or sets the server port.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets custom data to pass through to clients.
    /// Use this for auth tokens, session metadata, provider-specific extras, etc.
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; }
}

/// <summary>
/// Assignment data for CustomAssignment type.
/// Use this when connection is entirely custom (no IP/port).
/// </summary>
public class CustomAssignmentData : AssignmentData
{
    public override string Type => "Custom";

    /// <summary>
    /// Gets or sets custom data to pass through to clients.
    /// This is the primary payload for custom connections.
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; }
}
