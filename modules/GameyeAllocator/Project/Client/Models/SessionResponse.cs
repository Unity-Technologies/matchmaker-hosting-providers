using System.Collections.Generic;
using Newtonsoft.Json;

namespace GameyeAllocatorModule.Client.Models;

public class SessionResponse
{
	[JsonProperty("id")]
	public required string Id { get; set; }

	[JsonProperty("host")]
	public string? Host { get; set; }

	[JsonProperty("ports")]
	public List<PortMapping>? Ports { get; set; }

	[JsonProperty("status")]
	public string? Status { get; set; }
}

public class PortMapping
{
	[JsonProperty("type")]
	public string? Type { get; set; }

	[JsonProperty("container")]
	public int Container { get; set; }

	[JsonProperty("host")]
	public int Host { get; set; }
}
