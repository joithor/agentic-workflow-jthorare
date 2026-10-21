using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'effect.pr.rerun-checks' (PR babysitter spec §10.1): asks the forge to run the failed checks of the head again; 'rerunRequested' when it asked for some, 'rerunNone' when none ran.</summary>
[Lane(TestLane.Unit)]
public sealed class RerunChecksEffectTests
{
    private readonly InMemoryFactStore _facts = new();
    private readonly FakePullRequests _pulls = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private EffectContext Context() => new PrEffectRig(_facts).Context(
        "effect.pr.rerun-checks", "T-3/workflow.pr-babysitter/1:5:0",
        new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"", ["at"] = PrContexts.State() });

    [Fact]
    public async Task Failed_checks_on_the_head_are_run_again()
    {
        _pulls.Add(PrContexts.Pull());

        var done = await new RerunChecksEffect(_pulls, _facts).Dispatch(Context(), Token);

        Assert.Equal(OutcomeName.From("rerunRequested"), done.Value.Reply);
        Assert.Equal([(PrContexts.Link, PrContexts.Head)], _pulls.Reruns);
    }

    [Fact]
    public async Task A_forge_that_ran_none_again_replies_nothing()
    {
        _pulls.Add(PrContexts.Pull());
        _pulls.RerunAnswer = 0;

        var done = await new RerunChecksEffect(_pulls, _facts).Dispatch(Context(), Token);

        Assert.Equal(OutcomeName.From("rerunNone"), done.Value.Reply);
    }

    [Fact]
    public async Task An_unreachable_forge_is_an_error_the_activity_retries()
    {
        var result = await new RerunChecksEffect(_pulls, _facts).Dispatch(Context(), Token);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task A_missing_state_is_refused_without_retry()
    {
        var context = new PrEffectRig(_facts).Context("effect.pr.rerun-checks", "T-3/x/1:5:0", new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"" });

        var result = await new RerunChecksEffect(_pulls, _facts).Dispatch(context, Token);

        Assert.Equal(WorkflowErrors.NoRuntime.Code, result.Error.Code);
    }
}
