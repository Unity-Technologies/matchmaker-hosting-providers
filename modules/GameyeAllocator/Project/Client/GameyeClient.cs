using System;
using System.Net.Http;

namespace GameyeAllocatorModule.Client;

public interface IGameyeHttpClientFactory
{
	HttpClient Create(string apiToken);
}

public class GameyeHttpClientFactory : IGameyeHttpClientFactory
{
	// Static so the connection pool outlives a single invocation; a per-call handler re-handshakes every request.
	private static readonly SocketsHttpHandler SharedHandler = new()
	{
		PooledConnectionLifetime = TimeSpan.FromMinutes(5),
		PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
		MaxConnectionsPerServer = 300
	};

	public HttpClient Create(string apiToken)
	{
		// Cloud Code cancels an invocation at 15s; fail with budget left to return an error.
		var client = new HttpClient(SharedHandler, disposeHandler: false)
		{
			Timeout = TimeSpan.FromSeconds(10)
		};
		client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
		return client;
	}
}
