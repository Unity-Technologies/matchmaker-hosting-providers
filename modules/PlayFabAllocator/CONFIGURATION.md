# PlayFab Allocator Configuration

## Required Secrets

Add this secret in [Unity Dashboard](https://cloud.unity.com) → Administration → Secrets:

- `DEVELOPER_SECRET_KEY` - Your PlayFab developer secret key

Find this in [PlayFab Dashboard](https://developer.playfab.com/) → Settings → Secret Keys.

## Required Code Changes

Edit `Project/PlayFabAllocator.cs` and update these constants:

### PlayFabBuildId (Line 37)
```csharp
const string PlayFabBuildId = "MY_BUILD_ID"; // TODO: Replace with your PlayFab Build Id
```
Replace with your PlayFab build GUID from [PlayFab Dashboard](https://developer.playfab.com/) → Multiplayer → Servers → Builds.

### PlayFabTitleId (Line 42)
```csharp
const string PlayFabTitleId = "MY_TITLE_ID"; // TODO: Replace with your PlayFab Title Id
```
Replace with your PlayFab title ID from PlayFab Dashboard → Settings → API Features (e.g., `"A1B2C"`).

### DefaultPlayFabRegion (Line 47) - Optional
```csharp
const string DefaultPlayFabRegion = "EastUs";
```
