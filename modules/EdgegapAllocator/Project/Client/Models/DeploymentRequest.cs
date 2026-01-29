using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace EdgegapAllocatorModule.Client.Models;

public class DeploymentRequest
{
	[JsonProperty("application")]
	public required string Application { get; set; }

	[JsonProperty("version")]
	public required string Version { get; set; }

	[JsonProperty("require_cached_locations")]
	[DefaultValue(false)]
	public bool RequireCachedLocations { get; set; }

	[JsonProperty("users")]
	public List<DeploymentUser>? Users { get; set; }

	[JsonProperty("environment_variables")]
	public List<EnvironmentVariable>? EnvironmentVariables { get; set; }

	[JsonProperty("tags")]
	public List<string>? Tags { get; set; }
}

public class DeploymentUser
{
	[JsonProperty("user_type")]
	public required string UserType { get; set; }

	[JsonProperty("user_data")]
	public required UserData UserData { get; set; }
}

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class UserData
{
	// For user_type = "ip_address"
	[JsonProperty("ip_address")]
	public string? IpAddress { get; set; }

	// For user_type = "geo_coordinates"
	[JsonProperty("latitude")]
	public double? Latitude { get; set; }

	[JsonProperty("longitude")]
	public double? Longitude { get; set; }
}

public class EnvironmentVariable
{
	[JsonProperty("key")]
	public required string Key { get; set; }

	[JsonProperty("value")]
	public required string Value { get; set; }

	[JsonProperty("is_hidden")]
	[DefaultValue(false)]
	public bool IsHidden { get; set; }
}