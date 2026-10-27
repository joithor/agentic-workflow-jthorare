using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>
/// The two structure measures a map build runs on its joined graph before it writes a draft (map structure spec §4): the share of leaf home features
/// (a feature that holds files) with at least <see cref="MinFilesIsolated"/> files that link to no other feature and no table, and the share over
/// <see cref="SizeCap"/> files. Deterministic edges only. "At the limit" passes; the limits are proposals until the real map is re-measured.
/// </summary>
public static class MapChecks
{
    /// <summary>The most files a leaf home feature may hold before it counts as over the cap.</summary>
    public const int SizeCap = 150;

    /// <summary>The fewest files a feature needs to be measured for isolation.</summary>
    public const int MinFilesIsolated = 5;

    /// <summary>The fewest such features a map needs for the isolation measure to apply.</summary>
    public const int MinFeaturesIsolated = 20;

    /// <summary>Isolated features above this many tenths of a percent make the map degraded.</summary>
    public const int IsolatedDegradedPermille = 300;

    /// <summary>Isolated features above this many tenths of a percent refuse the map.</summary>
    public const int IsolatedRefusePermille = 600;

    /// <summary>Features over the cap above this many tenths of a percent make the map degraded.</summary>
    public const int OverCapDegradedPermille = 20;

    /// <summary>Features over the cap above this many tenths of a percent refuse the map.</summary>
    public const int OverCapRefusePermille = 100;

    /// <summary>Measures a built graph.</summary>
    /// <param name="graph">The joined graph a build is about to save.</param>
    /// <returns>The measures.</returns>
    public static MapChecksReport Measure(GraphSnapshot graph) => throw new NotSupportedException();
}
