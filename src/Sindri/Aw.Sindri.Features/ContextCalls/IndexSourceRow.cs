namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One source's progress (owned-index spec §5.8).</summary>
/// <param name="Source">The source id, e.g. 'github'.</param>
/// <param name="Phase">'recent', 'backfill', 'incremental' or 'reconcile', or null for a source that never committed a page.</param>
/// <param name="Usable">The recent window is synced.</param>
/// <param name="Complete">The backfill is done (the embeddings are not counted yet).</param>
/// <param name="LastSync">When the last tick of this feed ended since the process started, or null when none has.</param>
/// <param name="LastCode">The code that tick ended with (a down source's SND-CONTEXT-010), or null when it completed or none has run.</param>
public sealed record IndexSourceRow(string Source, string? Phase, bool Usable, bool Complete, DateTimeOffset? LastSync, string? LastCode);
