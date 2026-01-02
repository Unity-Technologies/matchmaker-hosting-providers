# Agones allocator configuration

## Required secrets

No secrets required by default. The allocator uses anonymous authentication, but you should configure proper authentication for production use.

## Required code changes

Edit `Project/AgonesAllocator.cs` and update these constants:

### AllocatorServiceBaseUrl (line 21)

```csharp
private const string AllocatorServiceBaseUrl = "AGONES_BASE_URL"; // TODO: Replace with Agones Allocator Service URL
```

Replace with your Agones Allocator Service URL. This should be the base URL of your Agones allocation endpoint (typically `https://your-agones-allocator.example.com`).

Find this URL from your Agones installation. Refer to the [Agones Allocator Service documentation](https://agones.dev/site/docs/advanced/allocator-service/) for setup instructions.

### Authentication Provider (line 31) - recommended for production

```csharp
var authProvider = new AnonymousAuthenticationProvider(); // TODO: Replace with required auth of your service
```

Replace `AnonymousAuthenticationProvider` with your preferred authentication method. For production deployments, consider using:
- mTLS (mutual TLS) authentication - recommended by Agones
- Bearer token authentication
- Custom authentication provider

Refer to the [Agones Allocator Service documentation](https://agones.dev/site/docs/advanced/allocator-service/) for authentication options.

### Allocation Request Configuration (line 41) - optional

```csharp
var allocation = await client.Gameserverallocation.PostAsync(new AllocationAllocationRequest
{
    // TODO: Add allocation selectors as needed
});
```

Configure allocation selectors to control which game servers are allocated. Common options include:

- `Namespace` - The Kubernetes namespace containing your GameServer fleet
- `GameServerSelectors` - Label selectors to filter available game servers
- `Scheduling` - Strategy for allocation ("Packed" or "Distributed")
- `Metadata` - Labels and annotations to add to allocated game servers

Refer to the [Agones GameServer Allocation documentation](https://agones.dev/site/docs/reference/gameserverallocation/) for all available options.
