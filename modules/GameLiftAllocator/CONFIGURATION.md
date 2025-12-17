# GameLift Allocator Configuration

## Required Secrets

Add these secrets in [Unity Dashboard](https://cloud.unity.com) → Administration → Secrets:

- `AWS_ACCESS_KEY_ID` - Your AWS access key ID
- `AWS_SECRET_ACCESS_KEY` - Your AWS secret access key

Find these in [AWS IAM Console](https://console.aws.amazon.com/iam/) → Users → Security Credentials → Create Access Key.

## Required Code Changes

Edit `Project/GameLiftAllocator.cs` and update these constants:

### GameSessionQueueName (Line 31)
```csharp
private const string GameSessionQueueName = "MyQueue"; // TODO: Replace with actual queue name
```
Replace with your AWS GameLift queue name from the [GameLift Console](https://console.aws.amazon.com/gamelift/) → Queues.

### DefaultAwsRegion (Line 33) - Optional
```csharp
private const string DefaultAwsRegion = "eu-west-2";
```
Valid values: `us-east-1`, `us-west-2`, `eu-west-1`, `eu-west-2`, `ap-southeast-1`, `ap-northeast-1`, etc.

### DefaultMaximumPlayerSessionCount (Line 32) - Optional
```csharp
private const int DefaultMaximumPlayerSessionCount = 10;
```
Set to your expected maximum players per game session.
