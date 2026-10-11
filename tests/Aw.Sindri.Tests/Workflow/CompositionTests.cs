using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Child workflows: their ids across parent attempts, one correlation per tree, the cancel cascade, and a child's end reaching a parent that was held.</summary>
public sealed class CompositionTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_second_parent_attempt_starts_its_own_child_and_drives_it()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var first = await h.Start("workflow.parent");
        _ = (await h.Signal(first, "go")).Value;
        var firstChild = await h.OnlyChild(first.Id);
        _ = (await h.Apply(first.Id, new CancelCommand("start over"))).Value;

        var second = await h.Start("workflow.parent", attempt: 2);
        var running = await h.Signal(second, "go");
        var secondChild = await h.OnlyChild(second.Id);
        var moved = await h.Signal(secondChild, "passed", ("checks.verdict", "pass"));

        Assert.True(running.IsSuccess, running.IsError ? running.Error.Message : null);
        Assert.Equal("running", running.Value.Instance.State);
        Assert.NotEqual(firstChild.Id, secondChild.Id);
        Assert.False(moved.Value.AlreadyRecorded);
        Assert.Equal("review", (await h.Get(secondChild.Id)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_event_of_a_two_level_tree_shares_the_root_correlation()
    {
        await using var h = new WorkflowHarness();
        h.Add(TestRecipes.Grand);
        _ = await h.HoldLease();
        var grand = await h.Start("workflow.grand");
        _ = (await h.Signal(grand, "go")).Value;
        var parent = await h.OnlyChild(grand.Id);
        _ = (await h.Signal(parent, "go")).Value;
        var leaf = await h.OnlyChild(parent.Id);

        _ = (await h.Signal(leaf, "passed", ("checks.verdict", "pass"))).Value;

        var correlations = h.Store.Events.Select(e => e.Envelope.CorrelationId).Distinct().ToList();
        _ = Assert.Single(correlations);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_the_root_cancels_the_whole_tree()
    {
        await using var h = new WorkflowHarness();
        h.Add(TestRecipes.Grand);
        _ = await h.HoldLease();
        var grand = await h.Start("workflow.grand");
        _ = (await h.Signal(grand, "go")).Value;
        var parent = await h.OnlyChild(grand.Id);
        _ = (await h.Signal(parent, "go")).Value;
        var leaf = await h.OnlyChild(parent.Id);

        _ = (await h.Apply(grand.Id, new CancelCommand("item closed"))).Value;

        Assert.Equal(InstanceStatus.Cancelled, (await h.Get(parent.Id)).Status);
        Assert.Equal(InstanceStatus.Cancelled, (await h.Get(leaf.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_that_finished_while_its_parent_was_parked_completes_the_parent_on_unpark()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = await h.OnlyChild(parent.Id);
        _ = (await h.Apply(parent.Id, new ParkCommand(ParkReason.NeedsAnswer, "a question"))).Value;
        _ = (await h.Signal(child, "passed", ("checks.verdict", "pass"))).Value;
        _ = (await h.Signal(child, "approved")).Value;

        var unparked = (await h.Apply(parent.Id, new UnparkCommand(Spend.Zero))).Value;

        Assert.Equal(InstanceStatus.Done, (await h.Get(child.Id)).Status);
        Assert.Equal(InstanceStatus.Done, unparked.Instance.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_that_finished_while_its_parent_was_taken_over_completes_the_parent_on_handback()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = await h.OnlyChild(parent.Id);
        _ = (await h.Apply(parent.Id, new TakeOverCommand())).Value;
        _ = (await h.Signal(child, "passed", ("checks.verdict", "pass"))).Value;
        _ = (await h.Signal(child, "approved")).Value;

        var handed = (await h.Apply(parent.Id, new HandBackCommand())).Value;

        Assert.Equal(InstanceStatus.Done, handed.Instance.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_top_rung_gate_failing_on_child_done_escalates_the_parent()
    {
        await using var h = new WorkflowHarness();
        h.Add(TestRecipes.Parent.Replace("on: child.done }", "on: child.done, gates: [gate.test.model] }", StringComparison.Ordinal));
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;
        var child = await h.OnlyChild(parent.Id);
        _ = (await h.Signal(child, "passed", ("checks.verdict", "pass"))).Value;

        _ = (await h.Signal(child, "approved")).Value;

        var escalated = await h.Get(parent.Id);
        Assert.Equal(InstanceStatus.Escalated, escalated.Status);
        Assert.Contains("gate.test.model", escalated.Hold?.Detail, StringComparison.Ordinal);
    }
}
