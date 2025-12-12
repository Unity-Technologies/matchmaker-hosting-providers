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

Select the hosting provider that you want to use under `modules/<provider_name>`

Update the remote config variables in: `modules/<provider_name>/Config.rc`

```sh
# will deploy remote config, matchmaker and cloud code files
ugs deploy modules/<provider_name> 
```

Navigate to the [Unity Dashboard](https://cloud.unity.com).
- Add the required secrets to your project in the under `Administration -> Secrets`
- Update your matchmaker to use the new cloud code based allocator under `Matchmaker -> Queues`

# Converting to public repository
Any and all Unity software of any description (including components) (1) whose source is to be made available other than under a Unity source code license or (2) in respect of which a public announcement is to be made concerning its inner workings, may be licensed and released only upon the prior approval of Legal.
