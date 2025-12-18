# PlayFab allocator configuration

## Required secrets

Add this secret in the [Unity Dashboard](https://cloud.unity.com) under **Administration** > **Secrets**:

- `PLAYFAB_SECRET_KEY` - Your PlayFab secret key

Find this in the [PlayFab Dashboard](https://developer.playfab.com/) under **Settings** > **Secret Keys**.

## Required code changes

Edit `Project/PlayFabAllocator.cs` and update these constants:

### PlayFabBuildId (line 37)

```csharp
const string PlayFabBuildId = "MY_BUILD_ID"; // TODO: Replace with your PlayFab Build Id
```

Replace with your PlayFab build GUID from the [PlayFab Dashboard](https://developer.playfab.com/) under **Multiplayer** > **Servers** > **Builds**.

### PlayFabTitleId (line 42)

```csharp
const string PlayFabTitleId = "MY_TITLE_ID"; // TODO: Replace with your PlayFab Title Id
```
Replace with your PlayFab title ID from the [PlayFab Dashboard](https://developer.playfab.com/) under **Settings** > **API Features**.

### DefaultPlayFabRegion (line 47) - optional

```csharp
const string DefaultPlayFabRegion = "EastUs";
```
