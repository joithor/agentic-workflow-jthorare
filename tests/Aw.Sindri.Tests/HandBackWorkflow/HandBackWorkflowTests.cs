using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.HandBackWorkflow;
using Aw.Sindri.Features.TakeOverWorkflow;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.HandBackWorkflow;

public sealed class HandBackWorkflowTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task HandBackWorkflow_resumes_the_timer_with_the_time_that_was_left()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        var taken = (await new TakeOverWorkflowService(h.Engine).Handle(new TakeOverWorkflowRequest(i.Id), Token)).Value.Instance;
        h.Clock.Advance(TimeSpan.FromHours(3));

        var back = (await new HandBackWorkflowService(h.Engine).Handle(new HandBackWorkflowRequest(i.Id), Token)).Value.Instance;

        Assert.Equal(InstanceStatus.Active, back.Status);
        Assert.Equal(h.Clock.GetUtcNow() + taken.Paused, back.Deadline);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task HandBackWorkflow_of_an_instance_not_taken_over_is_SND_WF_005()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();

        var refused = await new HandBackWorkflowService(h.Engine).Handle(new HandBackWorkflowRequest(i.Id), Token);

        Assert.Equal(SindriErrors.TransitionNotAllowed.Code, refused.Error.Code);
    }
}
