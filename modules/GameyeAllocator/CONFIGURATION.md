# Gameye Allocator Configuration

## Prerequisites

Refer to the [Gameye Getting Started guide](https://www.gameye.com/docs/getting-started/) to set up your account, Docker Hub registry, and application in the Gameye Admin Panel.

## Secrets

Add the following secret in the Unity Dashboard under **Administration > Secrets**:

| Secret Name | Description |
|---|---|
| `GAMEYE_API_TOKEN` | Your Gameye API bearer token. Obtain this from your Gameye account or by contacting Gameye support. |

## Code Configuration

All configuration is set in `ModuleConfig.Setup()` in `Project/GameyeAllocator.cs`, where a `GameyeAllocatorConfig` instance is registered with dependency injection. Open that file and edit the values in the config block.

### `Environment`

Controls which Gameye API endpoint the allocator targets.

| Value | API URL |
|---|---|
| `GameyeEnvironment.Sandbox` (default) | `https://api.sandbox-gameye.gameye.net` |
| `GameyeEnvironment.Production` | `https://api-production-gameye.gameye.net` |

Use `Sandbox` during development and testing. Switch to `Production` before going live.

> ⚠️ **`Environment` defaults to `Sandbox`.** If you go live without setting `Environment = GameyeEnvironment.Production`, every allocation silently targets sandbox infrastructure. As a safeguard, the allocator logs a **warning on every allocation while running in Sandbox** (and an info line confirming `PRODUCTION` once switched) — watch your Cloud Code logs to confirm the environment before launch.

### `ImageName` (required)

The name of your application image as configured in the Gameye Admin Panel. Must match exactly.

### `DefaultLocation`

Your preferred default deployment region (e.g. `"europe"`, `"us-east-1"`). Used when the matched pool has no entry in `LocationByPool`. See [available locations](https://www.gameye.com/docs/api-v2/available-locations/) for the full list. Defaults to `"europe"`.

### `LocationByPool` (optional)

Maps Unity Matchmaker **pool names** to Gameye location IDs, enabling dynamic region selection per match. When the matched pool is found in this dictionary, that location is sent to Gameye instead of `DefaultLocation`. Pools not in the map fall through to `DefaultLocation`.

Unity Matchmaker can be configured to use pools for region routing — create one pool per region in your queue configuration, then mirror that mapping here.

```csharp
LocationByPool = new Dictionary<string, string>
{
    { "eu-west-pool",    "eu-west"        },
    { "us-central-pool", "us-central"     },
    { "ap-ne-pool",      "asia-northeast" },
    { "sa-east-pool",    "southamerica"   },
},
```

Leave empty (the default) to always use `DefaultLocation`.

### `GamePort`

The primary container port your game server listens on (e.g. `7777`). This must match the port exposed in your Dockerfile and configured in the Gameye Admin Panel. This is the port passed to Unity Matchmaker's `AssignmentData.IpPort()`.

### `AdditionalPorts` (optional)

A dictionary of additional named ports to include in allocation data. Use this when your game server exposes secondary ports (e.g. a query port, voice port, or RCON port).

Each entry is returned in `AllocationData` as `port_{name}` so game clients can access them.

```csharp
AdditionalPorts = new Dictionary<string, int>
{
    { "query", 27015 },
    { "rcon", 27020 },
},
```

### `Version` (optional)

A specific Docker image tag / version to use when starting sessions. When `null` (the default), Gameye uses the highest-priority tag configured in the Admin Panel.

```csharp
Version = "v1.2.3",
```

## Example Configuration

```csharp
config.Dependencies.AddSingleton(new GameyeAllocatorConfig
{
    ImageName = "my-fps-server",
    Environment = GameyeEnvironment.Production,
    DefaultLocation = "eu-west",
    GamePort = 7777,
    Version = "v2.1.0",
    LocationByPool = new Dictionary<string, string>
    {
        { "eu-west-pool",    "eu-west"    },
        { "us-central-pool", "us-central" },
        { "ap-ne-pool",      "asia-northeast" },
    },
    AdditionalPorts = new Dictionary<string, int>
    {
        { "query", 27015 },
    },
});
```

## How It Works

Unlike other allocators that require a separate poll step, Gameye returns the host IP and port synchronously in the allocation response. The poll method is implemented for compatibility with Unity Matchmaker's allocation flow but will typically resolve on the first call.

## Resources

- [Gameye Documentation](https://www.gameye.com/docs/)
- [Session API Reference](https://www.gameye.com/docs/api-v2/reference/post-session/)
- [Available Locations](https://www.gameye.com/docs/api-v2/available-locations/)
- [Gameye Discord](https://discord.com/invite/QvJ3KH5max)
