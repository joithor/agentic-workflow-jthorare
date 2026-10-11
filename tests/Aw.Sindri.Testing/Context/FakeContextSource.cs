using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Testing.Context;

/// <summary>An in-memory source over seeded documents; passes <see cref="ContextSourceContract"/>. A test can make it fail or hang.</summary>
/// <param name="kind">Which source it plays.</param>
/// <param name="documents">The seed.</param>
public sealed class FakeContextSource(SourceKind kind, IReadOnlyList<SeedDocument> documents) : IContextSource
{
    /// <summary>When set, every search returns this error.</summary>
    public Error? Failure { get; set; }

    /// <summary>When true, a search waits until it is cancelled (a source past its deadline).</summary>
    public bool Hang { get; set; }

    /// <summary>The queries it was asked, in order.</summary>
    public IList<SourceQuery> Asked { get; } = [];

    /// <inheritdoc/>
    public SourceKind Kind => kind;

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        Asked.Add(query);
        if (Hang)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return SindriErrors.ContextSourceFailed.With("the search was cancelled");
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ContextSourceFailed.With("the search was cancelled");
        }

        return Failure is { } failure
            ? failure
            : Results.Success<IReadOnlyList<SourceHit>>(
                [.. FakeSearch.Run(documents, query.Terms, query.Limit).Select(m => new SourceHit(m.Document.Name, m.Line, m.Document.Title, Snippets.Line(m.Text)))]);
    }
}
