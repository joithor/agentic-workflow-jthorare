using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Loops, budgets, gates, evidence, outputs, universal states and timers through the engine.</summary>
public sealed class EngineBehaviorTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task A_loop_past_its_cap_parks_with_rounds()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        _ = (await h.Signal(i.Id, Failed("f1"))).Value;
        _ = (await h.Signal(i.Id, Failed("f2"))).Value;
        var third = (await h.Signal(i.Id, Failed("f3"))).Value.Instance;

        Assert.Equal((ParkReason.Rounds, "fix"), (third.Hold?.Reason, third.Hold?.Loop));
        Assert.Equal(2, third.Loops["fix"]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_diff_round_parks_with_no_progress()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var parked = (await h.Signal(i.Id, Failed("f1") with { EmptyDiff = true })).Value.Instance;

        Assert.Equal(ParkReason.NoProgress, parked.Hold?.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Spending_past_the_budget_parks_instead_of_transitioning()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var parked = (await h.Signal(i.Id, Failed("f1") with { Spend = new Spend(1500, 1) })).Value.Instance;
        var granted = (await h.Engine.Apply(i.Id, new UnparkCommand(new Spend(1000, 0)), Token)).Value.Instance;
        var moved = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value.Instance;

        Assert.Equal((ParkReason.Budget, "work", 1500L), (parked.Hold?.Reason, parked.State, parked.Spent.Tokens));
        Assert.Equal(1000, granted.Extra.Tokens);
        Assert.Equal("review", moved.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_gate_below_the_top_rung_refuses_and_records_nothing()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var refused = await h.Signal(i, "passed", ("checks.verdict", "fail"));

        Assert.Equal(SindriErrors.GateRefused.Code, refused.Error.Code);
        Assert.Contains("gate.checks.passed", refused.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single((await h.Store.History(i.Id, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Evidence_counts_only_when_registered_for_the_head_sha()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value;
        var head = HeadSha.From("abc1234");
        _ = (await h.Engine.Apply(i.Id, new RegisterEvidenceCommand("test", HeadSha.From("def5678"), "sha256:old"), Token)).Value;

        var missing = await h.Signal(i.Id, Tested(head));
        _ = (await h.Engine.Apply(i.Id, new RegisterEvidenceCommand("test", head, "sha256:new"), Token)).Value;
        var done = (await h.Signal(i.Id, Tested(head))).Value.Instance;

        Assert.Equal(SindriErrors.EvidenceMissing.Code, missing.Error.Code);
        Assert.Equal(InstanceStatus.Done, done.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Registering_the_same_evidence_again_records_nothing()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var evidence = new RegisterEvidenceCommand("test", HeadSha.From("abc1234"), "sha256:one");

        var first = (await h.Engine.Apply(i.Id, evidence, Token)).Value;
        var again = (await h.Engine.Apply(i.Id, evidence, Token)).Value;

        Assert.Equal((false, true), (first.AlreadyRecorded, again.AlreadyRecorded));
        Assert.Equal(first.Instance.Version, again.Instance.Version);
        Assert.Equal(2, (await h.Store.History(i.Id, Token)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_output_the_step_does_not_declare_is_refused()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var undeclared = await h.Signal(i, "passed", ("triage.size", "S"));
        var mistyped = await h.Signal(i, "passed", ("checks.verdict", "maybe"));
        var unknown = await h.Signal(i, "shipped");

        Assert.Equal(SindriErrors.OutputInvalid.Code, undeclared.Error.Code);
        Assert.Equal(SindriErrors.OutputInvalid.Code, mistyped.Error.Code);
        Assert.Equal(SindriErrors.TransitionNotAllowed.Code, unknown.Error.Code);
        Assert.Contains("allowed: passed, failed", unknown.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_a_parent_cancels_its_children_and_refuses_later_signals()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = Assert.Single((await h.Store.Children(parent.Id, Token)).Value);

        _ = (await h.Engine.Apply(parent.Id, new CancelCommand("item closed"), Token)).Value;
        var refused = await h.Signal(child, "passed", ("checks.verdict", "pass"));

        Assert.Equal(InstanceStatus.Cancelled, (await h.Get(parent.Id)).Status);
        Assert.Equal(InstanceStatus.Cancelled, (await h.Get(child.Id)).Status);
        Assert.Equal(SindriErrors.TransitionNotAllowed.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_changed_under_an_instance_refuses_it()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.Add(TestRecipes.Loop.Replace("cap: 2", "cap: 3", StringComparison.Ordinal));

        var refused = await h.Signal(i, "passed", ("checks.verdict", "pass"));

        Assert.Equal(SindriErrors.RecipeChanged.Code, refused.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_timeout_escalates_and_a_clock_jump_moves_timers_first()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var review = (await h.Signal(i, "passed", ("checks.verdict", "pass"))).Value.Instance;
        h.Clock.Advance(TimeSpan.FromHours(2));

        var jumped = (await h.Engine.Tick(TimeSpan.FromHours(2), Token)).Value;
        var moved = await h.Get(i.Id);
        h.Clock.Advance(TimeSpan.FromHours(1));
        var fired = (await h.Engine.Tick(clockJump: null, Token)).Value;

        Assert.Equal((1, 0), (jumped.Extended, jumped.Fired));
        Assert.Equal(review.Deadline + TimeSpan.FromHours(2), moved.Deadline);
        Assert.Equal(1, fired.Fired);
        Assert.Equal(InstanceStatus.Escalated, (await h.Get(i.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_lease_nothing_is_written()
    {
        await using var h = new WorkflowHarness();

        var refused = await h.Engine.Start(RecipeId.From("workflow.loop"), ItemId.From("ITEM-1"), 1, baseSha: null, Token);

        Assert.Equal(SindriErrors.NotRunning.Code, refused.Error.Code);
        Assert.Empty(h.Store.Events);
    }

    private static WorkflowSignal Failed(string finding) =>
        new("failed", ExpectedVersion: null, Sha: null, new Dictionary<string, string>(StringComparer.Ordinal) { ["checks.verdict"] = "fail" }, [finding], EmptyDiff: false, Spend.Zero);

    private static WorkflowSignal Tested(HeadSha sha) => new("tested", ExpectedVersion: null, sha, new Dictionary<string, string>(StringComparer.Ordinal), [], EmptyDiff: false, Spend.Zero);
}
