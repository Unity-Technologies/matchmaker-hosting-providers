using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace EdgegapAllocatorModule.Client.Models;

public class DeploymentStatusResponse
{
	[JsonProperty("request_id")]
	public required string RequestId { get; set; }

	[JsonProperty("fqdn")]
	public string? Fqdn { get; set; }

	[JsonProperty("current_status")]
	[JsonConverter(typeof(StatusConverter))]
	public DeploymentStatus CurrentStatus { get; set; }

	[JsonProperty("running")]
	public bool Running { get; set; }

	[JsonProperty("start_time")]
	public string? StartTime { get; set; }

	[JsonProperty("error")]
	public bool Error { get; set; }

	[JsonProperty("error_detail")]
	public string? ErrorDetail { get; set; }

	[JsonProperty("ports")]
	public Dictionary<string, Port>? Ports { get; set; }

	[JsonProperty("public_ip")]
	public string? PublicIp { get; set; }

	[JsonProperty("location")]
	public Location? Location { get; set; }

	[JsonProperty("tags")]
	public List<string>? Tags { get; set; }


	[JsonProperty("max_duration")]
	public int MaxDuration { get; set; }
}

public enum DeploymentStatus
{
	Ready,
	Error,
	Terminated,
	Deploying,
	Seeking,
	Terminating
}

public class StatusConverter : JsonConverter<DeploymentStatus>
{
	public override DeploymentStatus ReadJson(
		JsonReader reader,
		Type objectType,
		DeploymentStatus existingValue,
		bool hasExistingValue,
		JsonSerializer serializer
	)
	{
		var raw = reader.Value?.ToString();

		if (string.IsNullOrWhiteSpace(raw))
			throw new JsonSerializationException("Status value is null or empty");

		// Strip "Status." prefix if present
		var normalized = raw.Replace("Status.", "", StringComparison.OrdinalIgnoreCase);

		return normalized.ToUpperInvariant() switch
		{
			"READY" => DeploymentStatus.Ready,
			"ERROR" => DeploymentStatus.Error,
			"TERMINATED" => DeploymentStatus.Terminated,
			"DEPLOYING" => DeploymentStatus.Deploying,
			"SEEKING" => DeploymentStatus.Seeking,
			"TERMINATING" => DeploymentStatus.Terminating,
			_ => throw new JsonSerializationException($"Unknown deployment status '{raw}'")
		};
	}

	public override void WriteJson(
		JsonWriter writer,
		DeploymentStatus value,
		JsonSerializer serializer
	)
	{
		// Serialize back in Edgegap format
		writer.WriteValue($"Status.{value.ToString().ToUpperInvariant()}");
	}
}

public class Port
{
	[JsonProperty("external")]
	public int External { get; set; }

	[JsonProperty("internal")]
	public int Internal { get; set; }

	[JsonProperty("protocol")]
	public string? Protocol { get; set; }

	[JsonProperty("name")]
	public string? Name { get; set; }

	[JsonProperty("link")]
	public string? Link { get; set; }
}

public class Location
{
	[JsonProperty("city")]
	public string? City { get; set; }

	[JsonProperty("country")]
	public string? Country { get; set; }

	[JsonProperty("continent")]
	public string? Continent { get; set; }

	[JsonProperty("administrative_division")]
	public string? AdministrativeDivision { get; set; }

	[JsonProperty("timezone")]
	public string? Timezone { get; set; }

	[JsonProperty("latitude")]
	public double Latitude { get; set; }

	[JsonProperty("longitude")]
	public double Longitude { get; set; }
}