using Aw.Kernel;
using Aw.Context.Core.Facade;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Reads each feed's progress through the context engine's facade.</summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade.</param>
internal sealed class GetIndexStatusService(ContextScopes scopes, IContextEngine engine) : IService<GetIndexStatusRequest, IndexStatusView>
{
    public ValueTask<Result<IndexStatusView>> Handle(GetIndexStatusRequest request, CancellationToken cancellationToken)
    {
        _ = (scopes, engine);
        throw new NotSupportedException("red");
    }
}
