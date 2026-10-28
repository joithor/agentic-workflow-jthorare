using Aw.Connectors.Core.GitHub;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

internal sealed class PullGitHubService(IGitHubReader reader) : IService<PullGitHubRequest, PullGitHubResponse>
{
    public async ValueTask<Result<PullGitHubResponse>> Handle(PullGitHubRequest request, CancellationToken cancellationToken)
    {
        var page = await reader.Issues(request.Repo, request.After, request.Limit, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var items = page.Value.Items.Select(issue => GitHubRecords.From(request.Repo, issue)).ToList();
        return new PullGitHubResponse(items, page.Value.Next, page.Value.RateLimitedUntil);
    }
}
