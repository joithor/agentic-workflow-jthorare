using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Sync;

/// <summary>One pull from a feed.</summary>
/// <param name="After">The persisted cursor, or null from the start.</param>
/// <param name="Limit">At most this many items.</param>
/// <param name="NotBefore">The window's start ('windowDays', spec §5.5): a record last updated before it is not returned; null for no bound.</param>
public sealed record RecordPull(SourceCursor? After, int Limit, DateTimeOffset? NotBefore);
