using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.TakeOverWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.TakeOverWorkflow;

public sealed class TakeOverWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task TakeOverWorkflow_pauses_the_timer_and_marks_it_taken_over()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var taken = (await new TakeOverWorkflowService(h.Engine).Handle(new TakeOverWorkflowRequest(i.Id), Token)).Value.Instance;

        Assert.Equal(InstanceStatus.TakenOver, taken.Status);
        Assert.Null(taken.Deadline);
        _ = Assert.NotNull(taken.Paused);
    }
}
