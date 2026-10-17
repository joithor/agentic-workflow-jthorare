using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxOpen;

internal sealed class OnyxOpenService(IOnyxWorkspace workspace) : IService<OnyxOpenRequest, OnyxOpenResponse>
{
    public ValueTask<Result<OnyxOpenResponse>> Handle(OnyxOpenRequest request, CancellationToken cancellationToken)
    {
        _ = workspace;
        throw new NotSupportedException();
    }
}
