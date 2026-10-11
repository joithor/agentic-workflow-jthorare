using Aw.Kernel;

namespace Aw.Sindri.Core.Context;

/// <summary>
/// A context source (port; factory addendum §17.3 component interfaces): read-only search over one system. The contract
/// (ContextSourceContract in Aw.Sindri.Testing): hits best first, at most the query's limit, matching ignores case, no
/// match is an empty success, and a source that can't answer (unreachable, cancelled, past its deadline) returns
/// SND-CONTEXT-010 instead of throwing. Hits are raw text: the engine scrubs them once.
/// </summary>
public interface IContextSource
{
    /// <summary>Which source this is.</summary>
    public SourceKind Kind { get; }

    /// <summary>Searches the source.</summary>
    /// <param name="query">The terms and the limit.</param>
    /// <param name="cancellationToken">Cancels the search (the query's deadline).</param>
    /// <returns>The hits, best first, or SND-CONTEXT-010.</returns>
    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken);
}
