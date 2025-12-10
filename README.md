# matchmaker-hosting-providers
[View this project in Unity Internal Developer Portal](https://developer.portal.internal.unity.com/catalog/default/component/matchmaker-hosting-providers) <br/>

# Requirements

- [Dotnet SDK](https://dotnet.microsoft.com/download)
- [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/)
  - [Setup Project and Environment](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/setup-a-common-configuration/)
  - [Authenticate](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/get-authenticated/)

# Getting Started

Update the remote config variables in: `modules/MODULE_NAME/Config.rc`

Add the required secrets to your project in the [Unity Dashboard](https://cloud.unity.com) under `Administration -> Secrets`

```sh
# will deploy remote config, matchmaker and cloud code files
ugs deploy modules/MODULE_NAME 
```

# Converting to public repository
Any and all Unity software of any description (including components) (1) whose source is to be made available other than under a Unity source code license or (2) in respect of which a public announcement is to be made concerning its inner workings, may be licensed and released only upon the prior approval of Legal.
