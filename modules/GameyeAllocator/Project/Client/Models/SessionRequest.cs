using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameyeAllocatorModule.Client.Models;

public class SessionRequest
{
	[JsonProperty("id")]
	public string? Id { get; set; }

	[JsonProperty("location")]
	public required string Location { get; set; }

	[JsonProperty("image")]
	public required string Image { get; set; }

	[JsonProperty("env")]
	public Dictionary<string, string>? Env { get; set; }

	[JsonProperty("args")]
	public List<string>? Args { get; set; }

	[JsonProperty("labels")]
	public Dictionary<string, string>? Labels { get; set; }

	[JsonProperty("restart")]
	public bool Restart { get; set; }

	[JsonProperty("ttl")]
	public int? Ttl { get; set; }

	[JsonProperty("version", NullValueHandling = NullValueHandling.Ignore)]
	public string? Version { get; set; }
}