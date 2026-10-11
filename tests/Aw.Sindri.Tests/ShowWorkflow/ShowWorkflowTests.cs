using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ShowWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ShowWorkflow;

public sealed class ShowWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task ShowWorkflow_gives_the_state_kind_the_signals_it_takes_and_its_history()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var shown = (await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(i.Id), Token)).Value;

        Assert.Equal(("active", StateKind.Active), (shown.Status, shown.Kind));
        Assert.Equal(["passed", "failed"], shown.Signals);
        _ = Assert.Single(shown.History);
        Assert.Empty(shown.Children);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("cancel")]
    [InlineData("park")]
    public async Task ShowWorkflow_of_an_instance_that_takes_no_signals_lists_none(string hold)
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        WorkflowCommand command = hold == "cancel" ? new CancelCommand("dropped") : new ParkCommand(ParkReason.NeedsAnswer, "which API?");
        _ = (await h.Engine.Apply(i.Id, command, Token)).Value;

        var shown = (await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(i.Id), Token)).Value;

        Assert.Empty(shown.Signals);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ShowWorkflow_of_a_parent_lists_the_child_it_started()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var parent = await h.Start("workflow.parent");
        _ = (await h.Signal(parent, "go")).Value;

        var shown = (await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(parent.Id), Token)).Value;

        Assert.Equal(RecipeId.From("workflow.loop"), Assert.Single(shown.Children).Recipe);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ShowWorkflow_of_an_unknown_instance_is_SND_WF_004()
    {
        await using var h = new WorkflowHarness();

        var missing = await new ShowWorkflowService(h.Store, h.Catalog).Handle(new ShowWorkflowRequest(InstanceId.From("wf-000000000000")), Token);

        Assert.Equal(SindriErrors.InstanceUnknown.Code, missing.Error.Code);
    }
}
