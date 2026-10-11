using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ParkWorkflow;
using Aw.Sindri.Features.UnparkWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.UnparkWorkflow;

public sealed class UnparkWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task UnparkWorkflow_releases_the_hold_and_grants_the_extra_budget()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        _ = (await new ParkWorkflowService(h.Engine).Handle(new ParkWorkflowRequest(i.Id, ParkReason.Budget), Token)).Value;

        var unparked = (await new UnparkWorkflowService(h.Engine).Handle(new UnparkWorkflowRequest(i.Id, Tokens: 500, Turns: -3), Token)).Value.Instance;

        Assert.Equal((InstanceStatus.Active, "work"), (unparked.Status, unparked.State));
        Assert.Equal((500L, 0), (unparked.Extra.Tokens, unparked.Extra.Turns));
    }
}
