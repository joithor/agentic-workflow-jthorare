using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The decision ladder (§17.28.1): evidence first, then gates cheapest rung first, stopping at the first failure; only a top-rung failure after every lower rung passed escalates.</summary>
public sealed class GateLadderTests
{
    // The model gate is listed first; the ladder still runs the deterministic checks gate before it.
    private static readonly string _ladder = TestRecipes.Loop
        .Replace("gates: [gate.checks.passed] }", "gates: [gate.test.model, gate.checks.passed] }", StringComparison.Ordinal)
        .Replace("evidence: [test@head] }", "evidence: [test@head], gates: [gate.test.model] }", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_lower_rung_refuses_before_the_model_gate_runs()
    {
        await using var h = new WorkflowHarness();
        h.Add(_ladder);
        _ = await h.HoldLease();
        var i = await h.Start();

        var refused = await h.Signal(i, "passed", ("checks.verdict", "fail"));

        Assert.Equal(SindriErrors.GateRefused.Code, refused.Error.Code);
        Assert.Equal(0, h.Model.Calls);
        Assert.Equal(InstanceStatus.Active, (await h.Get(i.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_top_rung_failing_after_the_lower_ones_passed_escalates_with_every_rung_recorded_in_order()
    {
        await using var h = new WorkflowHarness();
        h.Add(_ladder);
        _ = await h.HoldLease();
        var i = await h.Start();

        var escalated = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value;

        Assert.Equal(InstanceStatus.Escalated, escalated.Instance.Status);
        Assert.Equal(["gate.checks.passed", "gate.test.model"], escalated.Gates.Select(g => g.Gate.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Missing_evidence_refuses_before_any_gate_runs()
    {
        await using var h = new WorkflowHarness();
        h.Add(_ladder);
        h.Model.Passes = true;
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value;
        var calls = h.Model.Calls;
        h.Model.Passes = false;

        var missing = await h.Signal(i.Id, new WorkflowSignal("tested", ExpectedVersion: null, HeadSha.From("abc1234"),
            new Dictionary<string, string>(StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero));

        Assert.Equal(SindriErrors.EvidenceMissing.Code, missing.Error.Code);
        Assert.Equal(calls, h.Model.Calls);
        Assert.Equal(InstanceStatus.Active, (await h.Get(i.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bad_output_refuses_before_any_gate_runs()
    {
        await using var h = new WorkflowHarness();
        h.Add(_ladder);
        _ = await h.HoldLease();
        var i = await h.Start();

        var refused = await h.Signal(i, "passed", ("checks.verdict", "maybe"));

        Assert.Equal(SindriErrors.OutputInvalid.Code, refused.Error.Code);
        Assert.Equal(0, h.Model.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_sees_the_recipe_attempt_and_the_transition_it_judges()
    {
        await using var h = new WorkflowHarness();
        h.Add(_ladder);
        h.Model.Passes = true;
        _ = await h.HoldLease();
        var i = await h.Start(attempt: 3);

        _ = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value;

        var seen = h.Model.Seen;
        Assert.NotNull(seen);
        Assert.Equal((RecipeId.From("workflow.loop"), 3, "work", "review", "passed"), (seen.Recipe, seen.Attempt, seen.State, seen.To, seen.On));
    }
}
