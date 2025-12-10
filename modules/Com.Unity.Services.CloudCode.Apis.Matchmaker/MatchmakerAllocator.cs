using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

public abstract class MatchmakerAllocator
{
    public abstract Task<AllocateResponse> Allocate(IExecutionContext context, IGameApiClient gameApiClient, AllocateRequest request);
    public abstract Task<PollResponse> Poll(IExecutionContext context, IGameApiClient gameApiClient, PollRequest request);
}
