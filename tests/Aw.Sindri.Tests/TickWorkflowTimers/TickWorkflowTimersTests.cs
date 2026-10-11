using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Features.TickWorkflowTimers;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.TickWorkflowTimers;

public sealed class TickWorkflowTimersTests
{
    private static CancellationToken Token => WorkflowHarness.Token;

    [Fact, Trait("Lane", "unit")]
    public async Task TickWorkflowTimers_fires_a_timer_past_its_deadline()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.Clock.Advance(TimeSpan.FromMinutes(11));

        var report = (await new TickWorkflowTimersService(h.Engine).Handle(new TickWorkflowTimersRequest(), Token)).Value;

        Assert.Equal(1, report.Fired);
        Assert.Equal(1, (await h.Get(i.Id)).Retries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task TickWorkflowTimers_after_a_clock_jump_extends_deadlines_instead_of_firing()
    {
        await using var h = new WorkflowHarness();
        _ = await h.HoldLease();
        var i = await h.Start();
        h.Clock.Advance(TimeSpan.FromMinutes(11));

        var report = (await new TickWorkflowTimersService(h.Engine).Handle(new TickWorkflowTimersRequest(TimeSpan.FromMinutes(11)), Token)).Value;

        Assert.Equal((0, 1), (report.Fired, report.Extended));
        Assert.Equal(InstanceStatus.Active, (await h.Get(i.Id)).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public void TickPace_reports_no_jump_for_a_steady_cadence()
    {
        var pace = new TickPace();
        var at = KnownInstants.Epoch;

        Assert.Null(pace.Observe(at));
        Assert.Null(pace.Observe(at.AddSeconds(5)));
        Assert.Null(pace.Observe(at.AddSeconds(10)));
        Assert.Null(pace.Observe(at.AddSeconds(16)));
    }

    [Fact, Trait("Lane", "unit")]
    public void TickPace_reports_the_extra_time_when_a_slot_comes_far_later_than_the_cadence()
    {
        var pace = new TickPace();
        var at = KnownInstants.Epoch;
        _ = pace.Observe(at);
        _ = pace.Observe(at.AddSeconds(5));

        var jump = pace.Observe(at.AddSeconds(5 + 3600));

        Assert.Equal(TimeSpan.FromSeconds(3595), jump);
    }

    [Fact, Trait("Lane", "unit")]
    public void TickPace_ignores_a_short_gap_even_if_it_is_several_cadences()
    {
        var pace = new TickPace();
        var at = KnownInstants.Epoch;
        _ = pace.Observe(at);
        _ = pace.Observe(at.AddSeconds(5));

        Assert.Null(pace.Observe(at.AddSeconds(5 + 25)));
    }
}
