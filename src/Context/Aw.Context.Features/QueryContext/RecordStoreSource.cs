using Aw.Context.Core.Audience;
using Aw.Context.Core.Search;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>The record store's lexical leg as a source.</summary>
internal sealed class RecordStoreSource(ILexicalLeg leg, AudienceScope scope) : IContextSource
{
    private static readonly SourceDescriptor _descriptor = new(Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false);

    public static SourceId Id { get; } = SourceId.From("records");

    public SourceDescriptor Describe() => _descriptor;

    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        _ = await leg.Search(query.Terms, scope, ILexicalLeg.DefaultLimit, cancellationToken).ConfigureAwait(false);
        return Array.Empty<SourceHit>();
    }

    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
