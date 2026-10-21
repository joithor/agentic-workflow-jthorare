using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Host.Forge;

namespace Aw.Sindri.Tests.Pr;

/// <summary>Until the forge wiring gives Sindri a forge, every call answers the typed error that says how to get one, and never defaults to GitHub.</summary>
[Lane(TestLane.Unit)]
public sealed class NoForgePullRequestsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_call_answers_no_runtime_with_the_fix()
    {
        var pulls = new NoForgePullRequests();

        var results = new[]
        {
            (await pulls.Get(PrContexts.Link, Token)).Error,
            (await pulls.CheckLog(PrContexts.Link, "build", Token)).Error,
            (await pulls.Push(new PullPush(PrContexts.Link, PrContexts.Tree, "c2", "c1"), Token)).Error,
            (await pulls.RerunChecks(PrContexts.Link, "c1", Token)).Error,
        };

        Assert.All(results, static error =>
        {
            Assert.Equal(WorkflowErrors.NoRuntime.Code, error.Code);
            Assert.Contains("workflow.forge", error.Message, StringComparison.Ordinal);
            Assert.Contains("sindri profile approve", error.Message, StringComparison.Ordinal);
        });
    }
}
