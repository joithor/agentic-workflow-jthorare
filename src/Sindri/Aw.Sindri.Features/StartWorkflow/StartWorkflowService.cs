using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.StartWorkflow;

internal sealed class StartWorkflowService(IWorkflowEngine engine) : IService<StartWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(StartWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Start(request.Recipe, request.Item, request.Attempt, request.BaseSha, cancellationToken);
}
