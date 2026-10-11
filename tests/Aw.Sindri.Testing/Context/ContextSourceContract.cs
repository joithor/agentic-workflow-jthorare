using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Xunit;

namespace Aw.Sindri.Testing.Context;

/// <summary>
/// The contract every <see cref="IContextSource"/> honours (docs/csharp/sindri-context.md): hits best first, at most the
/// limit, matching ignores case, no match is an empty success, every hit names its document and an excerpt holding a
/// term, and a cancelled search is SND-CONTEXT-010, never an exception.
/// </summary>
public abstract class ContextSourceContract
{
    private static readonly IReadOnlyList<SeedDocument> _corpus =
    [
        new("lease-fencing", "Host lease", "The host lease has an epoch.\nEvery write is fenced by the epoch.\nFencing stops a stale writer."),
        new("lease-only", "Lease notes", "Renew the lease every twenty seconds."),
        new("garden", "Garden", "Tomatoes need sun and water."),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SeedDocument Numbered(int i)
    {
        var n = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new SeedDocument("doc" + n, "Doc " + n, "lease number " + n);
    }

    /// <summary>The source under test, seeded with <paramref name="documents"/>.</summary>
    /// <param name="documents">The documents it must search.</param>
    /// <returns>The source.</returns>
    protected abstract Task<IContextSource> Create(IReadOnlyList<SeedDocument> documents);

    /// <summary>A document holding more of the terms ranks first; one holding none isn't returned.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Documents_holding_more_terms_rank_first()
    {
        var source = await Create(_corpus);

        var hits = (await source.Search(new SourceQuery(["lease", "epoch", "fencing"], 10), Token)).Value;

        Expect.Equal(2, hits.Count);
        Expect.True(hits[0].Document.Contains("lease-fencing", StringComparison.Ordinal));
        Expect.True(!hits.Any(h => h.Document.Contains("garden", StringComparison.Ordinal)));
    }

    /// <summary>No matching document is an empty success.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_match_is_an_empty_success()
    {
        var source = await Create(_corpus);

        var hits = await source.Search(new SourceQuery(["zzqxwibble"], 10), Token);

        Expect.True(hits.IsSuccess);
        Expect.Equal(0, hits.Value.Count);
    }

    /// <summary>The limit caps the hits.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_limit_caps_the_hits()
    {
        var source = await Create([.. Enumerable.Range(1, 5).Select(i => Numbered(i))]);

        var hits = (await source.Search(new SourceQuery(["lease"], 2), Token)).Value;

        Expect.Equal(2, hits.Count);
    }

    /// <summary>Matching ignores case, and every hit names a document, a title and an excerpt holding a term.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Hits_ignore_case_and_carry_document_title_and_excerpt()
    {
        var source = await Create([new SeedDocument("upper", "Upper", "THE LEASE EPOCH IS SHOUTED")]);

        var hit = Expect.Only((await source.Search(new SourceQuery(["lease"], 10), Token)).Value);

        Expect.True(hit.Document.Length > 0 && hit.Title.Length > 0);
        Expect.True(hit.Snippet.Contains("lease", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A cancelled search returns SND-CONTEXT-010 instead of throwing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancelled_search_is_a_failed_source_not_an_exception()
    {
        var source = await Create(_corpus);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var hits = await source.Search(new SourceQuery(["lease"], 10), cancelled.Token);

        Expect.True(hits.IsError);
        Expect.Equal(SindriErrors.ContextSourceFailed.Code, hits.Error.Code);
    }
}
