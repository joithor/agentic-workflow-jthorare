using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Context;

/// <summary>
/// Turns a question into search terms: words of letters, digits, '_' and '-' (so ILedgerStore and SND-LEASE-001 stay
/// whole), lower case, without common English words and one-letter words, at most <see cref="MaxTerms"/>, in order.
/// </summary>
public static class QueryTerms
{
    /// <summary>The most terms a question yields.</summary>
    public const int MaxTerms = 8;

    /// <summary>The longest question accepted, in characters.</summary>
    public const int MaxQuestionLength = 500;

    private static readonly HashSet<string> _stop = new(StringComparer.Ordinal)
    {
        "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "for", "from", "how", "in", "is", "it", "of", "on",
        "or", "the", "this", "that", "to", "was", "we", "what", "when", "where", "which", "who", "why", "with",
    };

    /// <summary>The terms of <paramref name="question"/>.</summary>
    /// <param name="question">The question as typed.</param>
    /// <returns>The terms, or SND-CONTEXT-003 when the question is empty, too long or has no searchable word.</returns>
    public static Result<IReadOnlyList<string>> From(string question)
    {
        if (question.Length > MaxQuestionLength)
        {
            return SindriErrors.ContextBadQuery.With($"the question is {question.Length} characters; the most is {MaxQuestionLength}");
        }

        var terms = Words(question)
            .Select(w => w.Trim('-').ToLowerInvariant())
            .Where(w => w.Length >= 2 && !_stop.Contains(w))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTerms)
            .ToList();
        if (terms.Count == 0)
        {
            return SindriErrors.ContextBadQuery.With($"'{question}' has no word to search for (fix: ask with at least one word of two or more letters)");
        }

        return terms;
    }

    private static IEnumerable<string> Words(string text)
    {
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-')
            {
                _ = word.Append(c);
            }
            else if (word.Length > 0)
            {
                yield return word.ToString();
                _ = word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }
}
