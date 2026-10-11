using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.StartWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.StartWorkflow;

public sealed class StartWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task StartWorkflow_puts_the_item_in_the_recipe_initial_state_once()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var service = new StartWorkflowService(h.Engine);
        var request = new StartWorkflowRequest(RecipeId.From("workflow.loop"), ItemId.From("ITEM-7"));

        var first = (await service.Handle(request, Token)).Value;
        var again = (await service.Handle(request, Token)).Value;

        Assert.Equal(("work", false), (first.Instance.State, first.AlreadyRecorded));
        Assert.Equal((first.Instance.Id, true), (again.Instance.Id, again.AlreadyRecorded));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StartWorkflow_of_an_unknown_recipe_is_SND_WF_002()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();

        var refused = await new StartWorkflowService(h.Engine).Handle(new StartWorkflowRequest(RecipeId.From("workflow.none"), ItemId.From("ITEM-7")), Token);

        Assert.Equal(SindriErrors.RecipeUnknown.Code, refused.Error.Code);
    }
}
