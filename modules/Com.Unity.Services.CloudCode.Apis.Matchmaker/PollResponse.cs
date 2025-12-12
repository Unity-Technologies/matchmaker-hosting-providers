using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
///     Response payload from the Cloud Code poll function.
/// </summary>
public class PollResponse
{
    public PollResponse(PollStatus status)
    {
        Status = status;
    }

    /// <summary>
    ///     The current status of the allocation.
    /// </summary>
    [JsonProperty("status")]
    public PollStatus Status { get; }

    /// <summary>
    ///     A human-readable message describing the current state.
    ///     Provides additional context for any status, especially useful for errors.
    /// </summary>
    [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; init; }

    /// <summary>
    ///     The assignment data containing connection details.
    ///     Required when Status is Allocated.
    /// </summary>
    [JsonProperty("assignmentData", NullValueHandling = NullValueHandling.Ignore)]
    public AssignmentData? AssignmentData { get; init; }
}

/// <summary>
///     Base class for Cloud Code assignment data.
///     Use the appropriate derived class based on AssignmentType.
/// </summary>
public class AssignmentData
{
    /// <summary>
    /// Create ip and port allocation assignment data.
    /// </summary>
    /// <param name="ip">The ip for the client to use.</param>
    /// <param name="port">The port for the client to use.</param>
    /// <param name="customData">Additional data for the client.</param>
    /// <returns>An assignment data instance.</returns>
    public static AssignmentData IpPort(string ip, int port, Dictionary<string, object>? customData = null)
    {
        return new AssignmentData(AssignmentType.IpPort)
        {
            Ip = ip,
            Port = port,
            CustomData = customData
        };
    }

    /// <summary>
    /// Create custom allocation assignment data.
    /// </summary>
    /// <param name="customData">Additional data for the client.</param>
    /// <returns>An assignment data instance.</returns>
    public static AssignmentData Custom(Dictionary<string, object>? customData)
    {
        return new AssignmentData(AssignmentType.Custom)
        {
            CustomData = customData
        };
    }

    private AssignmentData(AssignmentType type)
    {
        Type = type;
    }

    [JsonProperty("type")]
    public AssignmentType Type { get; }

    /// <summary>
    ///     The server IP address.
    /// </summary>
    [JsonProperty("ip", NullValueHandling = NullValueHandling.Ignore)]
    public string? Ip { get; private init; }

    /// <summary>
    ///     The server port.
    /// </summary>
    [JsonProperty("port", NullValueHandling = NullValueHandling.Ignore)]
    public int Port { get; private init; }

    /// <summary>
    ///     Custom data to pass through to clients.
    ///     Use this for auth tokens, session metadata, provider-specific extras, etc.
    /// </summary>
    [JsonProperty("customData", NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, object>? CustomData { get; private init; }
}

/// <summary>
/// Type of Assignment.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AssignmentType
{
    /// <summary>
    /// An Ip and Port assignment.
    /// </summary>
    [EnumMember(Value = "ipPort")]
    IpPort = 0,
    /// <summary>
    /// A custom assignment.
    /// </summary>
    [EnumMember(Value = "custom")]
    Custom = 1
}
