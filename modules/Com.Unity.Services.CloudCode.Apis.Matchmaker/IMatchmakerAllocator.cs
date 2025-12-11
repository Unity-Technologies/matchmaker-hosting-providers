using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis.Matchmaker;

/// <summary>
/// Custom matchmaker allocator
/// </summary>
public interface IMatchmakerAllocator
{
    /// <summary>
    /// Allocate will be called once every match.
    /// It should initiate the allocation and store any state needed in the response.
    /// </summary>
    /// <param name="context">Cloud Code Request context.</param>
    /// <param name="request">Allocation information about the match.</param>
    /// <returns>Results of the allocation</returns>
    public Task<AllocateResponse> Allocate(IExecutionContext context, AllocateRequest request);
    
    /// <summary>
    /// Poll will be called repeatedly by the matchmaker while the server is starting.
    /// Context about the match is passed through the request.
    /// </summary>
    /// <param name="context">Cloud Code Request context.</param>
    /// <param name="request">Information about the allocation to poll.</param>
    /// <returns>Results of the polling indicating the state of the server.</returns>
    public Task<PollResponse> Poll(IExecutionContext context, PollRequest request);
}
