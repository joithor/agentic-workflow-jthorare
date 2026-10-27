using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>The structure measures of one map: numbers for every build, and what they decide (map structure spec §4).</summary>
/// <param name="Isolated">Leaf home features with no link to another feature or a table.</param>
/// <param name="OverCap">Leaf home features holding more than <see cref="MapChecks.SizeCap"/> files.</param>
/// <param name="NoFileEdges">Whether the map has at least 20 features to measure but no file-level depends-on, reads or writes edge at all (imports and data stores not extracted).</param>
/// <param name="Offenders">The five largest features of each measure that trips.</param>
public sealed record MapChecksReport(CheckMeasure Isolated, CheckMeasure OverCap, bool NoFileEdges, IReadOnlyList<CheckOffender> Offenders)
{
    /// <summary>The worst verdict of the two measures.</summary>
    public CheckVerdict Verdict => throw new NotSupportedException($"not built: {Isolated.Name}");

    /// <summary>What these measures decide under <paramref name="mode"/>.</summary>
    /// <param name="mode">Whether a refusal stops the build.</param>
    /// <returns>The verdict, the error when refused, the build report's notes and the reason stored with the draft.</returns>
    public MapChecksOutcome Decide(ChecksMode mode) => throw new NotSupportedException($"not built: {Isolated.Name} under {mode}");
}
