using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The Linear record feed (spec 2026-10-27 sindri-owned index §5.2): issues newest first with their state and links.</summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long each call gets.</param>
internal sealed class LinearFeed(LinearClient client, TimeSpan deadline) : RecordFeed(SourceId.From("linear"))
{
    /// <inheritdoc />
    public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken) =>
        client is null || deadline < TimeSpan.Zero ? throw new ArgumentNullException(nameof(client)) : throw new NotSupportedException();
}
