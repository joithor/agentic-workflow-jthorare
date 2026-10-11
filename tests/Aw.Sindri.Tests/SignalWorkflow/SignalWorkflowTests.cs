using Aw.Sindri.Core;
using Aw.Sindri.Features.SignalWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.SignalWorkflow;

public sealed class SignalWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task SignalWorkflow_with_the_step_outputs_moves_the_instance_along_its_transition()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal) { ["checks.verdict"] = "pass" };

        var moved = (await new SignalWorkflowService(h.Engine).Handle(new SignalWorkflowRequest(i.Id, "passed", Outputs: outputs), Token)).Value;

        Assert.Equal("review", moved.Instance.State);
        Assert.Equal(i.Version + 1, moved.Instance.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SignalWorkflow_with_a_stale_expected_version_is_SND_WF_003()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var refused = await new SignalWorkflowService(h.Engine).Handle(new SignalWorkflowRequest(i.Id, "failed", ExpectedVersion: i.Version + 5), Token);

        Assert.Equal(SindriErrors.InstanceChanged.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SignalWorkflow_with_negative_spend_counts_it_as_zero()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var findings = new[] { "f1" };

        var moved = (await new SignalWorkflowService(h.Engine).Handle(new SignalWorkflowRequest(i.Id, "failed", Findings: findings, Tokens: -50, Turns: -1), Token)).Value;

        Assert.Equal((0L, 0), (moved.Instance.Spent.Tokens, moved.Instance.Spent.Turns));
    }
}
