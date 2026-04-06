using System.Net.Http;

namespace GameyeAllocatorModule.Client;

public interface IGameyeHttpClientFactory
{
	HttpClient Create(string apiToken);
}

public class GameyeHttpClientFactory : IGameyeHttpClientFactory
{
	public HttpClient Create(string apiToken)
	{
		var client = new HttpClient();
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
		return client;
	}
}
