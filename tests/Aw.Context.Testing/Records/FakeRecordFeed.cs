using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="RecordFeed"/>: a fixed list of items paged by position (the cursor is the next index); it passes <see cref="RecordFeedContract{TFeed}"/>.</summary>
/// <param name="source">The source it reads.</param>
/// <param name="items">Every item, in the order the feed serves them.</param>
public sealed class FakeRecordFeed(SourceId source, IReadOnlyList<RecordItem> items) : RecordFeed
{
    /// <inheritdoc />
    public override SourceId Source => source;

    /// <inheritdoc />
    public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken) => throw new NotSupportedException($"{items.Count} items");
}
