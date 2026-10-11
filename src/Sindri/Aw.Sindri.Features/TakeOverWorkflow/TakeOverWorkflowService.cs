using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.TakeOverWorkflow;

internal sealed class TakeOverWorkflowService(IWorkflowEngine engine) : IService<TakeOverWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(TakeOverWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new TakeOverCommand(), cancellationToken);
}
