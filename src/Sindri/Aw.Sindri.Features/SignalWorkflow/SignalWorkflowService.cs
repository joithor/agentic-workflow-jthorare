using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.SignalWorkflow;

internal sealed class SignalWorkflowService(IWorkflowEngine engine) : IService<SignalWorkflowRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(SignalWorkflowRequest request, CancellationToken cancellationToken)
    {
        var signal = new WorkflowSignal(request.On, request.ExpectedVersion, request.Sha,
            request.Outputs ?? new Dictionary<string, string>(StringComparer.Ordinal), request.Findings ?? [], request.EmptyDiff,
            new Spend(Math.Max(request.Tokens, 0), Math.Max(request.Turns, 0)));
        return engine.Signal(request.Instance, signal, cancellationToken);
    }
}
