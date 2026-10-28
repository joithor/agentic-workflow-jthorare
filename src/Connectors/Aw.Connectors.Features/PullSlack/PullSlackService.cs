using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>Reads each channel whole, newest first, and folds threads and day buckets into records (stub, red first).</summary>
internal sealed class PullSlackService : IService<PullSlackRequest, PullSlackResponse>
{
    public ValueTask<Result<PullSlackResponse>> Handle(PullSlackRequest request, CancellationToken cancellationToken) => throw new NotSupportedException(SlackRecords.AudienceKey("stub"));
}
