using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>Pure decisions: the no-progress rule and the universal-state refusals.</summary>
public sealed class DecisionTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(null, "a", false, false)]
    [InlineData("a", "a", false, true)]
    [InlineData("a", "a,b", false, true)]
    [InlineData("a,b", "a", false, false)]
    [InlineData("a", "b", false, false)]
    [InlineData(null, "a", true, true)]
    public void No_progress_is_the_same_findings_again_or_an_empty_diff(string? previous, string current, bool emptyDiff, bool detected) =>
        Assert.Equal(detected, NoProgress.Detected(previous?.Split(','), current.Split(','), emptyDiff));

    [Fact, Trait("Lane", "unit")]
    public void Universal_commands_refuse_what_the_state_does_not_allow()
    {
        var at = KnownInstants.Epoch;
        var active = AnInstance.New("ITEM-1", at);
        var cancelled = active with { Ended = InstanceEnd.Cancelled };
        var parked = active with { Hold = new InstanceHold(HoldKind.Parked, ParkReason.Rounds, "x") };

        Assert.True(CommandDecisions.HandBack(active, at).IsError);
        Assert.True(CommandDecisions.Cancel(cancelled, "again", at).IsError);
        Assert.True(CommandDecisions.Park(parked, new InstanceHold(HoldKind.Parked, ParkReason.Budget, "y"), at).IsError);
        Assert.True(CommandDecisions.TakeOver(active with { TakenOver = true }, at).IsError);
        Assert.Equal(ChangeKind.TakenOver, CommandDecisions.TakeOver(parked, at).Value.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Keys_name_recipe_item_attempt_transition_signal_version_and_sha()
    {
        var i = AnInstance.New("ITEM-1", KnownInstants.Epoch);

        Assert.Equal("wf:workflow.test:ITEM-1:1:work>done:passed@v1:abc1234", WorkflowKeys.Transition(i, "done", "passed", Core.Ids.HeadSha.From("abc1234")));
        Assert.Equal("wf:workflow.test:ITEM-1:1:start", WorkflowKeys.Start(i.Recipe, i.Item, 1));
        Assert.Equal(i.Id, WorkflowKeys.Instance("local", i.Recipe, i.Item, 1));
        Assert.NotEqual(i.Id, WorkflowKeys.Instance("local", i.Recipe, i.Item, 2));
    }
}
