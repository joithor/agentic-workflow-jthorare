using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.UnparkWorkflow;

internal sealed class UnparkWorkflowService(IWorkflowEngine engine) : IService<UnparkWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(UnparkWorkflowRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new UnparkCommand(new Spend(Math.Max(request.Tokens, 0), Math.Max(request.Turns, 0))), cancellationToken);
}
