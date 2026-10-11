using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.TickWorkflowTimers;

internal sealed class TickWorkflowTimersService(IWorkflowEngine engine) : IService<TickWorkflowTimersRequest, TickReport>
{
    public ValueTask<Result<TickReport>> Handle(TickWorkflowTimersRequest request, CancellationToken cancellationToken) =>
        engine.Tick(request.ClockJump, cancellationToken);
}
