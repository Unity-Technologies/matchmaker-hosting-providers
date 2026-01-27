using Newtonsoft.Json;

namespace EdgegapAllocatorModule.Client.Models;

public class DeploymentResponse
{
	[JsonProperty("request_id")]
	public required string RequestId { get; set; }
}