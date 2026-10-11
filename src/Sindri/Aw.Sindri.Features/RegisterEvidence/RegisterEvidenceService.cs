using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.RegisterEvidence;

internal sealed class RegisterEvidenceService(IWorkflowEngine engine) : IService<RegisterEvidenceRequest, WorkflowOutcome>
{
    public ValueTask<Result<WorkflowOutcome>> Handle(RegisterEvidenceRequest request, CancellationToken cancellationToken) =>
        engine.Apply(request.Instance, new RegisterEvidenceCommand(request.Kind, request.Sha, request.Digest), cancellationToken);
}
