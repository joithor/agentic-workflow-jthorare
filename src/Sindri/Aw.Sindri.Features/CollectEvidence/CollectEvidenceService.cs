using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.CollectEvidence;

/// <summary>
/// Runs the kind's evidence provider outside any engine transaction (a tool run can take minutes; the ledger's write lock is
/// never held across it), then registers a passing run through the engine. A failed run is reported and registers nothing.
/// The instance must exist and still be running before the tool runs.
/// </summary>
internal sealed class CollectEvidenceService(EvidenceProviders providers, IWorkflowStore store, IWorkflowEngine engine)
    : IService<CollectEvidenceRequest, CollectEvidenceResponse>
{
    public async ValueTask<Result<CollectEvidenceResponse>> Handle(CollectEvidenceRequest request, CancellationToken cancellationToken)
    {
        var provider = providers.Find(request.Kind);
        if (provider.IsError)
        {
            return provider.Error;
        }

        var found = await store.Get(request.Instance, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is null)
        {
            return found.IsError ? found.Error : SindriErrors.InstanceUnknown.With($"no workflow instance {request.Instance.Value} (fix: sindri workflow list)");
        }

        if (found.Value.Ended is not null)
        {
            return SindriErrors.TransitionNotAllowed.With($"{request.Instance.Value} is {WorkflowWords.Status(found.Value)}: it takes no more evidence");
        }

        var produced = await provider.Value.Provide(new EvidenceRequest(request.Instance, request.Sha, request.Inputs), cancellationToken).ConfigureAwait(false);
        if (produced.IsError)
        {
            return produced.Error;
        }

        var p = produced.Value;
        if (!p.Passed)
        {
            return new CollectEvidenceResponse(request.Kind, Passed: false, p.Detail, p.Digest, p.Document, Registered: null);
        }

        var registered = await engine.Apply(request.Instance, new RegisterEvidenceCommand(request.Kind, request.Sha, p.Digest), cancellationToken).ConfigureAwait(false);
        return registered.IsError
            ? registered.Error
            : new CollectEvidenceResponse(request.Kind, Passed: true, p.Detail, p.Digest, p.Document, registered.Value);
    }
}
