namespace Aw.Sindri.Core.Context;

/// <summary>
/// Reciprocal rank fusion across sources (factory addendum §17.3: fusion by RRF): an item scores 1/(k + rank) in each
/// list it appears in, with k = 60; only ranks count, never a source's own scores. Items are keyed by source and
/// locator, and no two sources share a locator yet, so today this interleaves the sources by rank (ties go to source
/// order, then locator); it starts fusing when the context graph gives records shared entity keys.
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
