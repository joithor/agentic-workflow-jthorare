using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.CancelWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.CancelWorkflow;

public sealed class CancelWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task CancelWorkflow_ends_the_instance_as_cancelled()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var cancelled = (await new CancelWorkflowService(h.Engine).Handle(new CancelWorkflowRequest(i.Id), Token)).Value;

        Assert.Equal(InstanceStatus.Cancelled, cancelled.Instance.Status);
        Assert.Equal(InstanceStatus.Cancelled, (await h.Get(i.Id)).Status);
    }
}
