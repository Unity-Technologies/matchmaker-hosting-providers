# Edgegap allocator configuration

Get started with [Edgegap's Documentation](https://docs.edgegap.com).

[Read more about switching from Multiplay.](https://docs.edgegap.com/docs/tools-and-integrations/switch-from-multiplay#start-deployments-from-ugs-matchmaker).

## Required secrets

Add these secrets in the [Unity Dashboard](https://cloud.unity.com) under **Administration** > **Secrets**:

- `EDGEGAP_API_TOKEN` - Your Edgegap API token.

Find your token in the [Edgegap Console](https://app.edgegap.com/user-settings?tab=tokens).

## Required code changes

Edit `Project/EdgegapAllocator.cs` and update these constants:

### ApplicationName (line 34)

```csharp
private const string ApplicationName = "MyApp"; // TODO: Replace with actual application name
```

Replace with your Edgegap application name from
the [Applications List](https://app.edgegap.com/application-management/applications/list).

### VersionName (line 35)

```csharp
private const string VersionName = "MyVersion"; // TODO: Replace with actual version name
```

Replace with your Edgegap application's version name you want to use.

### PortName (line 36)

```csharp
private const string PortName = "gameport"; // TODO: Replace with actual port name
```

Replace with your Edgegap application version's port name that will be used for players to connect.
