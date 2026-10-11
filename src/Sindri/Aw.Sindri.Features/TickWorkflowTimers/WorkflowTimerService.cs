using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.TickWorkflowTimers;

/// <summary>The 'workflow-timers' background service: the tick that fires due workflow timers on the profile's cadence.</summary>
/// <param name="tick">The tick use case.</param>
/// <param name="pace">The memory of earlier slots, for clock-jump detection.</param>
internal sealed class WorkflowTimerService(IService<TickWorkflowTimersRequest, TickReport> tick, TickPace pace) : IScheduledService
{
    internal static readonly ServiceName ServiceId = ServiceName.From("workflow-timers");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var ticked = await tick.Handle(new TickWorkflowTimersRequest(pace.Observe(run.Slot)), cancellationToken).ConfigureAwait(false);
        return ticked.Map(_ => Unit.Value);
    }
}
