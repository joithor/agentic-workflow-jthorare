using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Pr;

/// <summary>The in-memory pull requests hold to what every <see cref="IPullRequests"/> honours.</summary>
[Lane(TestLane.Unit)]
public sealed class FakePullRequestsContractTests : PullRequestsContract
{
    protected override ValueTask<(IPullRequests Pulls, string Link, string Workspace)> Create()
    {
        var pulls = new FakePullRequests();
        pulls.Add(PrContexts.Pull(head: "aaaa", checksSha: "aaaa"));
        pulls.SetLog(PrContexts.Link, "build", "boom");
        return ValueTask.FromResult<(IPullRequests, string, string)>((pulls, PrContexts.Link, PrContexts.Tree));
    }

    protected override ValueTask<string> Commit(string workspace) => ValueTask.FromResult("bbbb");
}
