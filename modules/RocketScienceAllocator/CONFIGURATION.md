# Multiplay by Rocket Science Allocator Configuration

## Required secrets

Add these secrets in the [Unity Dashboard](https://cloud.unity.com) under **Administration** > **Secrets**:

- `ROCKET_SCIENCE_MULTIPLAY_API_KEY` - Your Rocket Science by Multiplay API key, this key must have permission to read and write allocations.

## Required code changes

Edit `Project/RocketScienceAllocator.cs` and update these constants:

### FleetId

```csharp
private const string FleetId = "your_fleet_id";
```

Replace with your Multiplay by Rocket Science fleet ID from the [Dashboard](https://dashboard.multiplay.dev) under **Fleets**.

### BuildConfigId

```csharp
private const int BuildConfigId = 0;
```

Replace with your build configuration ID from Multiplay by Rocket Science under **Build Configurations**.

### DefaultRegion

```csharp
private const string DefaultRegion = "your_default_region";
```

Replace with your preferred region. Find available regions in Multiplay by Rocket Science under **Fleets** > **Region settings**.

## Optional code changes

### RocketScienceProjectID and RocketScienceEnvironmentID

```csharp
private const string RocketScienceProjectID = "";
private const string RocketScienceEnvironmentID = "";
```

By default, the allocator uses the Unity project ID and environment ID from the Cloud Code execution context — i.e. the same project and environment where the Cloud Code module is deployed. This is the typical case for customers who have migrated from Unity Multiplay to Multiplay by Rocket Science.

If your Multiplay by Rocket Science project and/or environment IDs differ from those in the Unity Dashboard, set these constants to override the context values. Leave them empty to use the context values.
