using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.QueryContext;

internal sealed class QueryContextService : IService<QueryContextRequest, QueryContextResponse>
{
    public ValueTask<Result<QueryContextResponse>> Handle(QueryContextRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: QueryContext");
}
