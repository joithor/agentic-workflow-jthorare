using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// The boundary every opened source sits behind: an exception an adapter didn't expect becomes SND-CONTEXT-010 (that
/// source failed, the answer is degraded), never a failed query. The caller's own cancellation still propagates.
/// </summary>
internal sealed class GuardedSource(IContextSource inner) : IContextSource
{
    public SourceKind Kind => inner.Kind;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.Search(query, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.ContextSourceFailed.With("the search was cancelled");
        }
        catch (Exception e)
        {
            return SindriErrors.ContextSourceFailed.With($"the {SourceNames.Of(inner.Kind)} source failed unexpectedly ({e.GetType().Name}: {e.Message})");
        }
    }
}
