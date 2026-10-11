using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ParkWorkflow;

internal sealed class ParkWorkflowService(IWorkflowEngine engine) : IService<ParkWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(ParkWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new ParkCommand(request.Reason, request.Detail ?? "parked by the human"), cancellationToken);
}
