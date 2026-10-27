using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

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

    private const int Listed = 5;

    /// <summary>Measures a built graph.</summary>
    /// <param name="graph">The joined graph a build is about to save.</param>
    /// <returns>The measures.</returns>
    public static MapChecksReport Measure(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var files = graph.Edges
            .Where(static e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature })
            .GroupBy(static e => e.To.Value, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.Select(static e => e.From.Value).Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);
        var linked = Linked(graph);
        var fileEdges = graph.Edges.Any(static e => e is { Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, Type: EdgeType.DependsOn or EdgeType.Reads or EdgeType.Writes });
        var measured = files.Where(static f => f.Value >= MinFilesIsolated).ToList();
        var noFileEdges = measured.Count >= MinFeaturesIsolated && !fileEdges;
        var isolated = measured.Where(f => !linked.Contains(f.Key)).ToList();
        var over = files.Where(static f => f.Value > SizeCap).ToList();
        var isolatedMeasure = measured.Count < MinFeaturesIsolated
            ? new CheckMeasure(IsolatedName, Count: 0, Total: 0, IsolatedDegradedPermille, IsolatedRefusePermille, Skipped: $"fewer than {MinFeaturesIsolated.ToString(System.Globalization.CultureInfo.InvariantCulture)} features of at least {MinFilesIsolated.ToString(System.Globalization.CultureInfo.InvariantCulture)} files")
            : noFileEdges
                ? new CheckMeasure(IsolatedName, Count: 0, Total: 0, IsolatedDegradedPermille, IsolatedRefusePermille, Skipped: "no file edges (imports and data stores not extracted)")
                : new CheckMeasure(IsolatedName, isolated.Count, measured.Count, IsolatedDegradedPermille, IsolatedRefusePermille);
        var overMeasure = new CheckMeasure(OverCapName, over.Count, files.Count, OverCapDegradedPermille, OverCapRefusePermille, files.Count == 0 ? "no home features" : null);
        return new MapChecksReport(isolatedMeasure, overMeasure, noFileEdges, [.. Largest(IsolatedName, isolatedMeasure.Skipped is null ? isolated : []), .. Largest(OverCapName, over)]);
    }

    /// <summary>The measure's name for the features with no link.</summary>
    internal const string IsolatedName = "isolated";

    /// <summary>The measure's name for the features over the cap.</summary>
    internal const string OverCapName = "over-cap";

    // The home features a deterministic roll-up edge touches: a depends-on between features (either end), or a reads or writes from one.
    private static HashSet<string> Linked(GraphSnapshot graph)
    {
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in graph.Edges.Where(static e => e.Provenance == Provenance.Deterministic))
        {
            switch (e)
            {
                case { Type: EdgeType.DependsOn, From.Kind: NodeKind.Feature, To.Kind: NodeKind.Feature }:
                    _ = linked.Add(e.From.Value);
                    _ = linked.Add(e.To.Value);
                    break;
                case { Type: EdgeType.Reads or EdgeType.Writes, From.Kind: NodeKind.Feature }:
                    _ = linked.Add(e.From.Value);
                    break;
                default:
                    break;
            }
        }

        return linked;
    }

    private static IEnumerable<CheckOffender> Largest(string measure, IEnumerable<KeyValuePair<string, int>> features) =>
        features.OrderByDescending(static f => f.Value).ThenBy(static f => f.Key, StringComparer.Ordinal).Take(Listed).Select(f => new CheckOffender(measure, f.Key, f.Value));
}
