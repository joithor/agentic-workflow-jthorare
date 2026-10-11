namespace Aw.Sindri.Core.Context;

/// <summary>
/// Reciprocal rank fusion across sources (factory addendum §17.3: fusion by RRF): an item scores 1/(k + rank) in each
/// source list it appears in, with k = 60; only ranks count, never a source's own scores. The same locator from one
/// source counts once (its best rank). Ties go to source order, then locator.
/// </summary>
public static class RankFusion
{
    /// <summary>The RRF constant.</summary>
    public const int K = 60;

    /// <summary>Fuses the per-source lists.</summary>
    /// <param name="lists">Each source's hits, best first.</param>
    /// <param name="limit">The most items to return.</param>
    /// <returns>The fused items, best first.</returns>
    public static IReadOnlyList<FusedHit> Fuse(IReadOnlyList<SourceList> lists, int limit)
    {
        var items = new Dictionary<(SourceKind, string), FusedHit>();
        foreach (var list in lists)
        {
            for (var i = 0; i < list.Hits.Count; i++)
            {
                var hit = list.Hits[i];
                var key = (list.Source, hit.Locator);
                var score = 1.0 / (K + i + 1);
                if (!items.TryGetValue(key, out var existing) || existing.Score < score)
                {
                    items[key] = new FusedHit(list.Source, hit, score, list.RetrievedAt);
                }
            }
        }

        return
        [
            .. items.Values
                .OrderByDescending(f => f.Score)
                .ThenBy(f => f.Source)
                .ThenBy(f => f.Hit.Locator, StringComparer.Ordinal)
                .Take(limit),
        ];
    }
}
