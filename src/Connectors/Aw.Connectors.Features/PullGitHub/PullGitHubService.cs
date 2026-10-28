using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

internal sealed class PullGitHubService : IService<PullGitHubRequest, PullGitHubResponse>
{
    public ValueTask<Result<PullGitHubResponse>> Handle(PullGitHubRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("PullGitHub: make PullGitHubTests pass");
}
