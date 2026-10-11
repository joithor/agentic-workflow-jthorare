using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.ParkWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ParkWorkflow;

public sealed class ParkWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task ParkWorkflow_holds_the_instance_in_its_state_with_the_reason()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var parked = (await new ParkWorkflowService(h.Engine).Handle(new ParkWorkflowRequest(i.Id, ParkReason.NeedsAnswer, "which API?"), Token)).Value.Instance;

        Assert.Equal((InstanceStatus.Parked, "work", ParkReason.NeedsAnswer), (parked.Status, parked.State, parked.Hold?.Reason));
    }
}
