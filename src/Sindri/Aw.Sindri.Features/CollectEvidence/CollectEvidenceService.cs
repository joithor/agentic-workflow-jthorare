using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CollectEvidence;

internal sealed class CollectEvidenceService : IService<CollectEvidenceRequest, CollectEvidenceResponse>
{
    public ValueTask<Result<CollectEvidenceResponse>> Handle(CollectEvidenceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: collect evidence");
}
