using Aw.Connectors.Features.PullGitHub;
using Aw.Connectors.Testing;

namespace Aw.Connectors.Tests.PullGitHub;

public sealed class PullGitHubTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task PullGitHub_pull_returns_the_first_page_of_issues()
    {
        var service = new PullGitHubService();
        var request = A.PullGitHubRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert pull_returns_the_first_page_of_issues");
    }
}
