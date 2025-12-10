using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Apis.Matchmaker;
using System.Threading.Tasks;

namespace HelloWorld;

public class MultiplayAllocator : MatchmakerAllocator
{
    public override Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request)
    {
        throw new System.NotImplementedException();
    }

    public override Task<PollResponse> Poll(IExecutionContext context, PollRequest request)
    {
        throw new System.NotImplementedException();
    }
}
