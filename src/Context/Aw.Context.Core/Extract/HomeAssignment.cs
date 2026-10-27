using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What <see cref="HomeFeatures.Assign"/> found: the home and group features with their links, and where each directory and file landed.</summary>
public sealed class HomeAssignment
{
    /// <summary>The features, the code nodes, the deterministic 'implements' edge of each file and the 'part-of' edge of each child group.</summary>
    public ExtractedMap Map { get; } = ExtractedMap.Empty;

    /// <summary>The feature keyed at each directory (a leaf's or a group's, never a '_' leaf), by repo-relative directory path ('' is the repo root).</summary>
    public IReadOnlyDictionary<string, NodeKey> Directories { get; } = new Dictionary<string, NodeKey>(StringComparer.Ordinal);

    /// <summary>The home feature of each homed file, by repo-relative path.</summary>
    public IReadOnlyDictionary<string, NodeKey> Files { get; } = new Dictionary<string, NodeKey>(StringComparer.Ordinal);

    /// <summary>What the labeler is asked to name: one entry per feature, with the paths below it.</summary>
    public IReadOnlyList<FileCluster> LabelRequests { get; } = [];

    /// <summary>The same assignment with the model's names on the features it named.</summary>
    /// <param name="labels">The labels the labeling pass gave, by the ids of <see cref="LabelRequests"/>.</param>
    /// <returns>The assignment; a feature the model did not name keeps its path-derived name.</returns>
    public HomeAssignment WithLabels(IReadOnlyList<ClusterLabel> labels) =>
        throw new NotSupportedException("red: HomeAssignment.WithLabels");
}
