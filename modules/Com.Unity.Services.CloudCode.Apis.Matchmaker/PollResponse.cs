using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Assignment types available for Cloud Code responses.
/// </summary>
public enum AssignmentType
{
    /// <summary>
    /// Modern IP/port-based assignment with CustomData support.
    /// Use this for third-party providers (GameLift, Edgegap, etc.).
    /// </summary>
    IpPortAssignment,

    /// <summary>
    /// Escape hatch for entirely custom connections (no IP/port).
    /// Use this when connection is handled entirely via CustomData.
    /// </summary>
    CustomAssignment,

    /// <summary>
    /// Backwards compatibility for existing Multiplay clients.
    /// Use this when clients expect the legacy MultiplayAssignment format.
    /// Note: Does NOT support CustomData.
    /// </summary>
    MultiplayAssignment,
}

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
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(IpPortAssignmentData), "IpPort")]
[JsonDerivedType(typeof(CustomAssignmentData), "Custom")]
public abstract class AssignmentData
{
}

/// <summary>
/// Assignment data for IpPortAssignment type.
/// Use this for modern third-party provider integrations.
/// </summary>
public class IpPortAssignmentData : AssignmentData
{
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
    /// <summary>
    /// Gets or sets custom data to pass through to clients.
    /// This is the primary payload for custom connections.
    /// </summary>
    public Dictionary<string, object> CustomData { get; set; }
}
