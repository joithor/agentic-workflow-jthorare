using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>What the store gave the build.</summary>
/// <param name="Map">The record nodes and their edges.</param>
/// <param name="Stamp">The store's record stamp as the build read it.</param>
/// <param name="Notes">The build report's lines, numbers only.</param>
internal sealed record MapRecordReading(ExtractedMap Map, long Stamp, IReadOnlyList<string> Notes)
{
    /// <summary>The records over <see cref="MapRecordSources.TouchCap"/> files: no file edges in <see cref="Map"/>, they roll up to features once the build has assigned the homes.</summary>
    public IReadOnlyList<RolledRecord> Overflow { get; init; } = [];
}
