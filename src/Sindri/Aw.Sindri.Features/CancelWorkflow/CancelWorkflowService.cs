using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.CancelWorkflow;

internal sealed class CancelWorkflowService(IWorkflowEngine engine) : IService<CancelWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(CancelWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new CancelCommand(request.Reason ?? "cancelled by the human"), cancellationToken);
}
