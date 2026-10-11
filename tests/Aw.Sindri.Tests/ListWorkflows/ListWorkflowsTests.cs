using Aw.Sindri.Features.CancelWorkflow;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ListWorkflows;

public sealed class ListWorkflowsTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task ListWorkflows_shows_open_instances_and_all_of_them_on_request()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var open = await h.Start(item: "ITEM-1");
        var ended = await h.Start(item: "ITEM-2");
        _ = (await new CancelWorkflowService(h.Engine).Handle(new CancelWorkflowRequest(ended.Id), Token)).Value;
        var service = new ListWorkflowsService(h.Store);

        var current = (await service.Handle(new ListWorkflowsRequest(), Token)).Value.Instances;
        var all = (await service.Handle(new ListWorkflowsRequest(All: true), Token)).Value.Instances;

        Assert.Equal(open.Id, Assert.Single(current).Id);
        Assert.Equal(2, all.Count);
    }
}
