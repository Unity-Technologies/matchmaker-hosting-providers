# Gameye Allocator Configuration

## Prerequisites

Refer to the [Gameye Getting Started guide](https://www.gameye.com/docs/getting-started/) to set up your account, Docker Hub registry, and application in the Gameye Admin Panel.

## Secrets

Add the following secret in the Unity Dashboard under **Administration > Secrets**:

| Secret Name | Description |
|---|---|
| `GAMEYE_API_TOKEN` | Your Gameye API bearer token. Obtain this from your Gameye account or by contacting Gameye support. |

## Code Configuration

Update the following constants in `Project/GameyeAllocator.cs`:

### `ImageName` (line 25)

Set this to the name of your application image as configured in the Gameye Admin Panel. This must match the image name you registered during setup.

### `DefaultLocation` (line 26)

Set this to your preferred default deployment region (e.g. `"europe"`, `"north-america"`). See [available locations](https://www.gameye.com/docs/api-v2/available-locations/) for the full list.

### `GamePort` (line 27)

Set this to the container port your game server listens on (e.g. `7777`). This must match the port exposed in your Dockerfile and configured in the Gameye Admin Panel.

## How It Works

Unlike other allocators that require a separate poll step, Gameye returns the host IP and port synchronously in the allocation response. The poll method is implemented for compatibility with Unity Matchmaker's allocation flow but will typically resolve on the first call.

## Resources

- [Gameye Documentation](https://www.gameye.com/docs/)
- [Session API Reference](https://www.gameye.com/docs/api-v2/reference/post-session/)
- [Available Locations](https://www.gameye.com/docs/api-v2/available-locations/)
- [Gameye Discord](https://discord.com/invite/QvJ3KH5max)
