# Matchmaker hosting providers

A collection of example integrations to connect Unity Matchmaker with various game server hosting providers.

## Purpose and scope

This repository demonstrates how to integrate [Unity Matchmaker](https://docs.unity.com/ugs/en-us/manual/matchmaker/manual/matchmaker-overview) with different game server hosting platforms using [Cloud Code modules](https://docs.unity.com/ugs/en-us/manual/cloud-code/manual). When using these examples, you're responsible for:

- Testing and validating integrations for your specific use case.
- Securing your deployments and managing credentials.
- Complying with each hosting provider's terms of service.
- Maintaining and updating your implementations.

## Requirements

- [Dotnet SDK](https://dotnet.microsoft.com/download)
- [UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/install-the-cli/)
  - [Set up project and environment](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/setup-a-common-configuration/)
  - [Authenticate](https://services.docs.unity.com/guides/ugs-cli/latest/general/get-started/get-authenticated/)
  - Make sure that your service account has the following project permissions:
    - `Unity Environments Viewer`
    - `Cloud Code Editor`

## Getting started

Select the hosting provider that you want to use under `modules/<provider_name>`:

- **AgonesAllocator** - Agones integration
- **GameLiftAllocator** - AWS GameLift integration
- **MultiplayAllocator** - Unity Multiplay integration
- **PlayFabAllocator** - Microsoft PlayFab integration

### Configure the module

Each module contains a `CONFIGURATION.md` file with detailed instructions on updating the required C# constants:

- [AgonesAllocator/CONFIGURATION.md](modules/AgonesAllocator/CONFIGURATION.md)
- [GameLiftAllocator/CONFIGURATION.md](modules/GameLiftAllocator/CONFIGURATION.md)
- [MultiplayAllocator/CONFIGURATION.md](modules/MultiplayAllocator/CONFIGURATION.md)
- [PlayFabAllocator/CONFIGURATION.md](modules/PlayFabAllocator/CONFIGURATION.md)

Once you've completed the configuration steps for your chosen provider, proceed to deploying the module.

### Deploy the module

```sh
# Deploy cloud code module for your chosen provider
ugs deploy modules/<provider_name>
```

### Configure Unity Dashboard

Navigate to the [Unity Dashboard](https://cloud.unity.com):

- Add the required secrets to your project under **Administration** > **Secrets** (refer to the respective module's `CONFIGURATION.md` for details)
- Update your Matchmaker to use the new Cloud Code-based allocator under **Matchmaker** > **Queues**.

## Maintenance and Support

This repository contains example code for educational and reference purposes. Unity will review updates to this repo but will not guarantee response times for issues or pull requests.

## Troubleshooting

If you have issues with the examples in this repository, refer to the documentation of your chosen hosting provider or seek help in the [Unity Discussions forums](https://discussions.unity.com/).

## Legal Notices

### Disclaimer and Warranty

THIS SOFTWARE IS PROVIDED "AS-IS" WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR UNITY BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY ARISING FROM THE USE OF THIS SOFTWARE.

**No Official Support**: This repository provides example code only. Unity does not provide official support, maintenance, or guarantees for these integrations. Use at your own risk.

### Provider Affiliation and Endorsement

Unity is not affiliated with, endorsed by, or in partnership with any of the hosting providers referenced in this repository. The inclusion of an integration example does not constitute an endorsement, recommendation, or guarantee of any provider's services.

Provider names are used in a descriptive, nominative manner to identify the integration targets. Users are responsible for:
- Evaluating provider suitability for their needs
- Complying with each provider's terms of service
- Managing their own provider relationships and agreements
- Understanding provider pricing, limitations, and policies

### Trademarks

All product names, logos, brands, trademarks, and registered trademarks are property of their respective owners. All company, product, and service names used in this repository are for identification purposes only.

- **Agones** is a trademark of Google LLC
- **Amazon GameLift** is a trademark of Amazon.com, Inc. or its affiliates
- **Microsoft PlayFab** is a trademark of Microsoft Corporation
- **Unity** and **Unity Multiplay** are trademarks of Unity Technologies ApS
- All other trademarks are the property of their respective owners

Use of these names does not imply any affiliation with or endorsement by the trademark holders.

### Third-Party Dependencies

This repository references third-party SDKs and services. You're responsible for:
- Complying with all third-party license terms
- Reviewing and accepting third-party service agreements
- Managing third-party dependencies and updates
- Understanding third-party attribution requirements

Refer to individual module documentation for specific SDK requirements and licensing information.

## License

This project is licensed under the Unity Companion License. Refer to the [LICENSE.md](LICENSE.md) file for details.

By using this software, you agree to the terms of the Unity Companion License, which permits use only in connection with the Unity game engine.
