using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Status values for Cloud Code allocation responses.
/// These values are returned by the Cloud Code allocate function to indicate the result of the allocation request.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AllocateStatus
{
    /// <summary>
    /// Allocation job was created successfully. The matchmaker will begin polling for completion.
    /// </summary>
    [EnumMember(Value = "created")]
    Created = 0,

    /// <summary>
    /// Allocation request failed. Check the Message property for details.
    /// </summary>
    [EnumMember(Value = "error")]
    Error = 1,
}
