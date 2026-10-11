namespace Aw.Sindri.Core.Context;

/// <summary>
/// Retrieval quality for one eval question (factory addendum §17.3 Proof): recall@k is the share of expected documents
/// found in the top k results, precision@k the share of the top k results that were expected. A result matches an
/// expected document when its document is that path or ends with '/' and that path.
/// </summary>
/// <param name="Recall">Recall at k, 0 to 1.</param>
/// <param name="Precision">Precision at k, 0 to 1 (0 when nothing was returned).</param>
public sealed record EvalScore(double Recall, double Precision)
{
    /// <summary>Scores <paramref name="retrieved"/> against <paramref name="expected"/>.</summary>
    /// <param name="expected">The documents a good answer cites (at least one).</param>
    /// <param name="retrieved">The documents returned, best first.</param>
    /// <param name="k">The cut-off.</param>
    /// <returns>The score.</returns>
    public static EvalScore Of(IReadOnlyList<string> expected, IReadOnlyList<string> retrieved, int k)
    {
        var top = retrieved.Take(k).ToList();
        var found = expected.Count(e => top.Exists(r => Matches(r, e)));
        var relevant = top.Count(r => expected.Any(e => Matches(r, e)));
        return new EvalScore(expected.Count == 0 ? 0 : (double)found / expected.Count, top.Count == 0 ? 0 : (double)relevant / top.Count);
    }

    /// <summary>The mean of several scores (0, 0 for none).</summary>
    /// <param name="scores">The scores.</param>
    /// <returns>Their mean recall and precision.</returns>
    public static EvalScore Mean(IReadOnlyList<EvalScore> scores) =>
        scores.Count == 0 ? new EvalScore(0, 0) : new EvalScore(scores.Average(s => s.Recall), scores.Average(s => s.Precision));

    private static bool Matches(string document, string expected) =>
        string.Equals(document, expected, StringComparison.Ordinal) || document.EndsWith("/" + expected, StringComparison.Ordinal);
}
