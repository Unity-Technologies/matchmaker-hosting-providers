using System.Net.Http;

namespace EdgegapAllocatorModule.Client;

public interface IEdgegapHttpClientFactory
{
	HttpClient Create(string apiToken);
}

public class EdgegapHttpClientFactory : IEdgegapHttpClientFactory
{
	public HttpClient Create(string apiToken)
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.Add("Authorization", $"{apiToken}");
		return client;
	}
}