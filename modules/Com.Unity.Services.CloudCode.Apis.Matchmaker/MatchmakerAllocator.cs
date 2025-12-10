using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

public abstract class MatchmakerAllocator
{
    public abstract Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request);
    public abstract Task<PollResponse> Poll(IExecutionContext context, PollRequest request);
}
