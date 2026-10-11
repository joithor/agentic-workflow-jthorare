using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.QueryContext;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.QueryContext;

public sealed class QueryContextTests
{
    private readonly ContextHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_returns_ranked_cited_results_from_every_source()
    {
        await _h.Approve(ContextHarness.CodeAndNotes());

        var answer = (await _h.Service.Handle(new QueryContextRequest("Where is the host lease epoch fenced?"), Token)).Value;

        Assert.Equal(["host", "lease", "epoch", "fenced"], answer.Terms);
        Assert.Equal(Enumerable.Range(1, answer.Results.Count), answer.Results.Select(r => r.Rank));
        Assert.Contains(answer.Results, r => r.Cite.Source == SourceKind.Code && string.Equals(r.Cite.Document, "src/Lease/SqliteLease.cs", StringComparison.Ordinal));
        Assert.Contains(answer.Results, r => r.Cite.Source == SourceKind.Notes && string.Equals(r.Cite.Document, "notes/lease.md", StringComparison.Ordinal));
        Assert.All(answer.Results, r => Assert.Equal(KnownInstants.Epoch, r.Cite.RetrievedAt));
        Assert.DoesNotContain(answer.Results, r => r.Cite.Document == "src/Garden.cs");
        Assert.Equal([SourceState.Ok, SourceState.Ok], answer.Sources.Select(s => s.State));
        Assert.Equal(answer.Results.Count, answer.Sources.Sum(s => s.Hits));
        Assert.False(answer.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_scrubs_every_result_before_it_leaves()
    {
        await _h.Approve(ContextHarness.CodeAndNotes());

        var answer = (await _h.Service.Handle(new QueryContextRequest("deploy token"), Token)).Value;

        var deploy = Assert.Single(answer.Results, r => r.Cite.Document == "notes/deploy.md");
        Assert.True(deploy.Redacted);
        Assert.Contains("[REDACTED:", deploy.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain(answer.Results, r => r.Snippet.Contains(TextScrubberContract.PlantedToken, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_searches_only_the_sources_asked_for()
    {
        await _h.Approve(ContextHarness.CodeAndNotes());

        var answer = (await _h.Service.Handle(new QueryContextRequest("host lease", Sources: ["NOTES"]), Token)).Value;

        Assert.All(answer.Results, r => Assert.Equal(SourceKind.Notes, r.Cite.Source));
        Assert.Empty(_h.CodeSource.Asked);
        var code = Assert.Single(answer.Sources, s => s.Source == SourceKind.Code);
        Assert.Equal((SourceState.Skipped, "not asked for"), (code.State, code.Detail));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("a", null, null)]
    [InlineData("   ", null, null)]
    [InlineData("host lease", "bogus", null)]
    [InlineData("host lease", null, 0)]
    [InlineData("host lease", null, 51)]
    public async Task QueryContext_refuses_a_bad_question_source_or_limit(string question, string? source, int? limit)
    {
        await _h.Approve(ContextHarness.CodeAndNotes());

        var answer = await _h.Service.Handle(new QueryContextRequest(question, source is null ? null : [source], limit), Token);

        Assert.Equal(SindriErrors.ContextBadQuery.Code, answer.Error.Code);
        Assert.Contains("fix:", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_needs_an_approved_profile()
    {
        var answer = await _h.Service.Handle(new QueryContextRequest("host lease"), Token);

        Assert.Equal(SindriErrors.ProfileNotApproved.Code, answer.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task QueryContext_needs_a_context_section_naming_a_source()
    {
        await _h.Approve(context: null);

        var answer = await _h.Service.Handle(new QueryContextRequest("host lease"), Token);

        Assert.Equal(SindriErrors.ContextNoSources.Code, answer.Error.Code);
        Assert.Contains("context:", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_source_degrades_the_answer_without_failing_it()
    {
        await _h.Approve(ContextHarness.CodeAndNotes());
        _h.CodeSource.Failure = SindriErrors.ContextSourceFailed.With("git grep exited 128");

        var answer = (await _h.Service.Handle(new QueryContextRequest("host lease"), Token)).Value;

        var code = Assert.Single(answer.Sources, s => s.Source == SourceKind.Code);
        Assert.Equal(SourceState.Failed, code.State);
        Assert.Equal("SND-CONTEXT-010 git grep exited 128", code.Detail);
        Assert.NotEmpty(answer.Results);
        Assert.True(answer.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_past_its_deadline_fails_and_the_others_still_answer()
    {
        await _h.Approve(ContextHarness.CodeAndNotes());
        _h.CodeSource.Hang = true;

        var pending = _h.Service.Handle(new QueryContextRequest("host lease"), Token).AsTask();
        while (_h.CodeSource.Asked.Count == 0)
        {
            await Task.Yield();
        }

        _h.Clock.Advance(ContextQueryDefaults.Deadline + TimeSpan.FromSeconds(1));
        var answer = (await pending).Value;

        var code = Assert.Single(answer.Sources, s => s.Source == SourceKind.Code);
        Assert.Equal(SourceState.Failed, code.State);
        Assert.StartsWith("SND-CONTEXT-010", code.Detail, StringComparison.Ordinal);
        Assert.All(answer.Results, r => Assert.Equal(SourceKind.Notes, r.Cite.Source));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_that_cannot_open_is_reported_skipped_with_its_reason()
    {
        await _h.Approve(ContextHarness.CodeAndNotes() with { Linear = new LinearContext() });

        var answer = (await _h.Service.Handle(new QueryContextRequest("host lease"), Token)).Value;

        var linear = Assert.Single(answer.Sources, s => s.Source == SourceKind.Linear);
        Assert.Equal(SourceState.Skipped, linear.State);
        Assert.StartsWith("SND-CONTEXT-002", linear.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_limit_comes_from_the_request_else_the_profile()
    {
        await _h.Approve(ContextHarness.CodeAndNotes(limit: 2));

        var fromProfile = (await _h.Service.Handle(new QueryContextRequest("lease epoch"), Token)).Value;
        var fromRequest = (await _h.Service.Handle(new QueryContextRequest("lease epoch", Limit: 1), Token)).Value;

        Assert.Equal(2, fromProfile.Results.Count);
        _ = Assert.Single(fromRequest.Results);
    }
}
