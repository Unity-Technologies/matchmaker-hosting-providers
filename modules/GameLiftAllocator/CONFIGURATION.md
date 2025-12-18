# GameLift allocator configuration

## Required secrets

Add these secrets in the [Unity Dashboard](https://cloud.unity.com) under **Administration** > **Secrets**:

- `AWS_ACCESS_KEY_ID` - Your AWS access key ID
- `AWS_SECRET_ACCESS_KEY` - Your AWS secret access key

Find these in the [AWS IAM Console](https://console.aws.amazon.com/iam/) under **Users** > **Security Credentials** > **Create Access Key**.

## Required code changes

Edit `Project/GameLiftAllocator.cs` and update these constants:

### GameSessionQueueName (line 31)

```csharp
private const string GameSessionQueueName = "MyQueue"; // TODO: Replace with actual queue name
```
Replace with your AWS GameLift queue name from the [GameLift Console](https://console.aws.amazon.com/gamelift/) under **Queues**.

### DefaultAwsRegion (line 33) - optional

```csharp
private const string DefaultAwsRegion = "eu-west-2";
```
Valid values: `us-east-1`, `us-west-2`, `eu-west-1`, `eu-west-2`, `ap-southeast-1`, `ap-northeast-1`, etc.

### DefaultMaximumPlayerSessionCount (line 32) - optional

```csharp
private const int DefaultMaximumPlayerSessionCount = 10;
```
Set to your expected maximum players per game session.
