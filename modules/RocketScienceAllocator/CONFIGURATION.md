# Multiplay by Rocket Science Allocator Configuration

## Required secrets

Add these secrets in the [Unity Dashboard](https://cloud.unity.com) under **Administration** > **Secrets**:

- `ROCKET_SCIENCE_MULTIPLAY_API_KEY` - Your Rocket Science by Multiplay API key, this key must have permission to read and write allocations.

You can create an API key in the [Multiplay Dashboard](https://dashboard.multiplay.dev) under **API Keys**. Ensure you give the API key `Server Write` and `Server Read` permissions.

## Required code changes

Edit `Project/RocketScienceAllocator.cs` and update these constants:

### FleetId

```csharp
private const string FleetId = "your_fleet_id";
```

Replace with the ID of the Multiplay by Rocket Science fleet you want to integrate with Unity Matchmaker. You can find this on the [Multiplay Dashboard](https://dashboard.multiplay.dev) under **Fleets**.

### BuildConfigId

```csharp
private const int BuildConfigId = 0;
```

Replace with the ID of the build configuration that you want to use when allocating game servers with Unity Matchmaker. You can find this in the [Multiplay Dashboard](https://dashboard.multiplay.dev) on the **Build Configurations** tab of your fleet.

### DefaultRegionId

```csharp
private const string DefaultRegionId = "your_default_region";
```

Replace with the ID of the region you want to use as the default or fallback when allocating game servers via the matchmaker. This will be used only in the case where your Unity Matchmaker ticket did not contain a requested region ID. You can find this in the [Multiplay Dashboard](https://dashboard.multiplay.dev) on the **Scaling settings** tab of your fleet.

## Optional code changes

### RocketScienceProjectID and RocketScienceEnvironmentID

```csharp
private const string RocketScienceProjectID = "";
private const string RocketScienceEnvironmentID = "";
```

By default, the allocator uses the Unity project ID and environment ID from the Cloud Code execution context — i.e. the same project and environment where the Cloud Code module is deployed. This is the typical case for customers who have migrated from Unity Multiplay to Multiplay by Rocket Science.

If your Multiplay by Rocket Science project and/or environment IDs differ from those in the Unity Dashboard, set these constants to override the context values. Leave them empty to use the context values.
