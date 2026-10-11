using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.HandBackWorkflow;

internal sealed class HandBackWorkflowService(IWorkflowEngine engine) : IService<HandBackWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(HandBackWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new HandBackCommand(), cancellationToken);
}
