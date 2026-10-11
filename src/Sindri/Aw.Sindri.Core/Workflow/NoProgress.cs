namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The no-progress rule (factory addendum §17.23.13): round n makes no progress over round n−1 when its open findings
/// equal or are a superset of the previous round's, or it produced an empty diff. A repeated failure signature is a
/// repeated finding, so it is covered by the first rule.
/// </summary>
public static class NoProgress
{
    /// <summary>Whether this round made no progress.</summary>
    /// <param name="previous">The previous round's findings, or null on the first round.</param>
    /// <param name="current">This round's findings.</param>
    /// <param name="emptyDiff">Whether this round produced an empty diff.</param>
    /// <returns>True when the loop should park with 'no-progress'.</returns>
    public static bool Detected(IReadOnlyList<string>? previous, IReadOnlyList<string> current, bool emptyDiff)
    {
        ArgumentNullException.ThrowIfNull(current);
        return emptyDiff || (previous is { Count: > 0 } && previous.All(f => current.Contains(f, StringComparer.Ordinal)));
    }
}
