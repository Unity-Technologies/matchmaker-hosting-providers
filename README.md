# Matchmaker Hosting Providers

A collection of examples to connect Unity Matchmaker with various game server hosting providers.

# Requirements

- [Dotnet SDK](https://dotnet.microsoft.com/download)
- [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/)
  - [Setup Project and Environment](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/setup-a-common-configuration/)
  - [Authenticate](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/get-authenticated/)
  - Make sure that your service account has the following project permissions:
    - `Unity Environments Viewer`
    - `Cloud Code Editor`

# Getting Started

Select the hosting provider that you want to use under `modules/<provider_name>`:
- **GameLiftAllocator** - AWS GameLift integration
- **MultiplayAllocator** - Unity Multiplay integration
- **PlayfabAllocator** - Microsoft PlayFab integration

## 1. Configure the Module

Each module contains a `CONFIGURATION.md` file with detailed instructions on updating the required C# constants:
- [GameLiftAllocator/CONFIGURATION.md](modules/GameLiftAllocator/CONFIGURATION.md)
- [MultiplayAllocator/CONFIGURATION.md](modules/MultiplayAllocator/CONFIGURATION.md)
- [PlayfabAllocator/CONFIGURATION.md](modules/PlayfabAllocator/CONFIGURATION.md)

## 2. Deploy the Module

```sh
# will deploy cloud code module
ugs deploy modules/<provider_name> 
```

## 3. Configure Unity Dashboard

Navigate to the [Unity Dashboard](https://cloud.unity.com):
- Add the required secrets to your project under `Administration -> Secrets` (see module's CONFIGURATION.md)
- Update your matchmaker to use the new cloud code based allocator under `Matchmaker -> Queues`
