
namespace Aw.Context.Core.Extract;

/// <summary>Rolls the file-level edges up to the home features (spec 2026-10-27 §3).</summary>
public static class FeatureRollup
{
    /// <summary>How many pairs (or files) a roll-up edge cites as evidence.</summary>
    public const int EvidencePairs = 5;

    /// <summary>Adds the feature-level 'depends-on', 'reads' and 'writes' edges the file-level ones imply.</summary>
    /// <param name="map">The joined extraction: home 'implements' edges, code-to-code 'depends-on' and code 'reads'/'writes' edges.</param>
    /// <returns>The map plus the roll-up edges.</returns>
    public static ExtractedMap Apply(ExtractedMap map) => throw new NotSupportedException();
}
