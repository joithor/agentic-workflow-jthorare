using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Tests.QueryContext;

/// <summary>The engine's pure rules: terms from a question, lexical ranking, rank fusion, excerpts and eval scores.</summary>
public sealed class RetrievalRulesTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("Where is the host lease epoch fenced?", "host,lease,epoch,fenced")]
    [InlineData("ILedgerStore and SND-LEASE-001", "iledgerstore,snd-lease-001")]
    [InlineData("lease LEASE Lease", "lease")]
    [InlineData("-dash- terms", "dash,terms")]
    public void A_question_becomes_lower_case_terms_without_common_words(string question, string expected) =>
        Assert.Equal(expected.Split(','), QueryTerms.From(question).Value);

    [Fact, Trait("Lane", "unit")]
    public void A_question_yields_at_most_eight_terms_and_a_long_one_is_refused()
    {
        Assert.Equal(QueryTerms.MaxTerms, QueryTerms.From("one two three four five six seven eight nine ten").Value.Count);
        Assert.Equal(SindriErrors.ContextBadQuery.Code, QueryTerms.From(new string('x', QueryTerms.MaxQuestionLength + 1)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Documents_rank_by_distinct_terms_then_matching_lines_then_name()
    {
        var matches = new LineMatches(["lease", "epoch"]);
        matches.Add("b.md", 1, "the lease");
        matches.Add("b.md", 2, "the lease again");
        matches.Add("a.md", 1, "a lease");
        matches.Add("c.md", 3, "nothing here");
        matches.Add("c.md", 4, "lease and EPOCH together");
        matches.Add("epoch-notes.md", 9, "lease");

        var ranked = matches.Ranked(10);

        Assert.Equal(["c.md", "epoch-notes.md", "b.md", "a.md"], ranked.Select(r => r.Document), StringComparer.Ordinal);
        Assert.Equal((4, "lease and EPOCH together"), (ranked[0].Line, ranked[0].Text));
        _ = Assert.Single(matches.Ranked(1));
    }

    [Fact, Trait("Lane", "unit")]
    public void Rank_fusion_interleaves_sources_by_rank_and_counts_a_locator_once()
    {
        var at = KnownInstants.Epoch;
        SourceList code = new(SourceKind.Code, [Hit("c1"), Hit("c2"), Hit("c1")], at);
        SourceList notes = new(SourceKind.Notes, [Hit("n1")], at);

        var fused = RankFusion.Fuse([code, notes], 10);

        Assert.Equal(["c1", "n1", "c2"], fused.Select(f => f.Hit.Document), StringComparer.Ordinal);
        Assert.Equal(1.0 / 61, fused[0].Score, 9);
        Assert.Equal(1.0 / 62, fused[2].Score, 9);
        Assert.Equal(2, RankFusion.Fuse([code, notes], 2).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_excerpt_is_the_first_line_holding_a_term_cut_to_two_hundred_characters()
    {
        Assert.Equal("the Lease line", Snippets.Around("first\n  the Lease line  \nlease again", ["lease"]));
        Assert.Equal("first", Snippets.Around("\nfirst\nsecond", ["zz"]));
        Assert.Equal(Snippets.MaxLength, Snippets.Line(new string('x', 500)).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Eval_scores_recall_and_precision_at_k_by_document_suffix()
    {
        var score = EvalScore.Of(["src/a.cs", "docs/b.md"], ["aw/src/a.cs", "aw/src/x.cs", "aw/docs/b.md", "aw/docs/c.md"], k: 3);

        Assert.Equal(1.0, score.Recall);
        Assert.Equal(2.0 / 3, score.Precision, 9);
        Assert.Equal(new EvalScore(0, 0), EvalScore.Of(["a"], [], 5));
        Assert.Equal(new EvalScore(0.5, 0.5), EvalScore.Mean([new EvalScore(1, 0), new EvalScore(0, 1)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Source_names_parse_ignoring_case_and_list_in_source_order()
    {
        Assert.True(SourceNames.TryParse(" ConnectAI ", out var kind));
        Assert.Equal(SourceKind.ConnectAi, kind);
        Assert.False(SourceNames.TryParse("slack", out _));
        Assert.Equal("code, notes, linear, connectai", SourceNames.All);
    }

    private static SourceHit Hit(string document) => new(document, 1, document, "excerpt");
}
