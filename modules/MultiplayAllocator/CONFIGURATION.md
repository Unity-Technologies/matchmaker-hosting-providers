# Multiplay allocator configuration

## Required secrets

No secrets required. This allocator uses the Unity service token for authentication.

## Required code changes

Edit `Project/MultiplayAllocator.cs` and update these constants:

### FleetId (line 25)

```csharp
private const string FleetId = "your_fleet_id";
```

Replace with your Unity Multiplay fleet ID from the [Unity Dashboard](https://cloud.unity.com) under **Multiplay** > **Fleets**.

### BuildConfigId (line 26)

```csharp
private const int BuildConfigId = 0;
```

Replace with your build configuration ID from Multiplay under **Build Configurations**.

### DefaultRegion (line 27)

```csharp
private const string DefaultRegion = "your_default_region";
```

Replace with your preferred region. Find available regions in Multiplay under **Fleets** > **Region settings**.
