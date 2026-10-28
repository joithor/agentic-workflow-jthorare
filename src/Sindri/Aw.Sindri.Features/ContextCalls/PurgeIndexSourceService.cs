using Aw.Kernel;
using Aw.Context.Core.Facade;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Counts, and on confirm deletes, one source's rows in the record store through the context engine's facade.</summary>
/// <param name="engine">Context's facade.</param>
internal sealed class PurgeIndexSourceService(IContextEngine engine) : IService<PurgeIndexSourceRequest, IndexPurgeView>
{
    public ValueTask<Result<IndexPurgeView>> Handle(PurgeIndexSourceRequest request, CancellationToken cancellationToken)
    {
        _ = engine;
        throw new NotSupportedException("red");
    }
}
