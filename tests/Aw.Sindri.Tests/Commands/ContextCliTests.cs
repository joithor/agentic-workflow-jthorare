using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Features.QueryContext;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

public sealed class ContextCliTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_arguments_read_into_a_request_in_any_order()
    {
        var (request, json) = ContextVerbs.Parse(["--json", "host lease", "--sources", "code, notes", "--limit", "3"]).Value;

        Assert.True(json);
        Assert.Equal("host lease", request.Question);
        Assert.Equal(["code", "notes"], request.Sources);
        Assert.Equal(3, request.Limit);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new string[0], "no question")]
    [InlineData(new[] { "q", "--limit", "x" }, "--limit needs a whole number")]
    [InlineData(new[] { "q", "--limit" }, "--limit needs a whole number")]
    [InlineData(new[] { "q", "--sources", "," }, "--sources needs one or more source names")]
    [InlineData(new[] { "q", "--colour" }, "'--colour' isn't a context query argument")]
    [InlineData(new[] { "q", "second" }, "'second' isn't a context query argument")]
    public void A_bad_argument_is_refused_with_the_usage(string[] args, string expected)
    {
        var error = ContextVerbs.Parse(args).Error;

        Assert.Equal(SindriErrors.ContextBadQuery.Code, error.Code);
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, false, 0)]
    [InlineData(1, true, 0)]
    [InlineData(0, false, 1)]
    [InlineData(0, true, 2)]
    public void The_exit_code_is_0_with_results_1_without_and_2_when_nothing_could_be_searched(int results, bool degraded, int expected)
    {
        var hit = new ContextHit(1, "t", "s", 0.1, Redacted: false, new Citation(SourceKind.Code, "aw/a.cs", 1, KnownInstants.Epoch));
        var report = new SourceReport(SourceKind.Code, degraded ? SourceState.Failed : SourceState.Ok, results, Detail: null);
        var answer = new QueryContextResponse("q", ["q"], results == 0 ? [] : [hit], [report]);

        Assert.Equal(expected, ContextVerbs.ExitCode(answer));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(SourceState.Ok, SourceState.Failed, 1)]
    [InlineData(SourceState.Skipped, SourceState.Skipped, 2)]
    [InlineData(SourceState.Skipped, SourceState.Failed, 2)]
    public void No_results_exit_1_when_a_source_answered_and_2_when_none_did(SourceState notes, SourceState linear, int expected)
    {
        var answer = new QueryContextResponse("q", ["q"], [],
            [new SourceReport(SourceKind.Notes, notes, 0, Detail: null), new SourceReport(SourceKind.Linear, linear, 0, Detail: null)]);

        Assert.Equal(expected, ContextVerbs.ExitCode(answer));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-CONTEXT-003", 1)]
    [InlineData("SND-CONTEXT-001", 2)]
    [InlineData("SND-PROFILE-005", 2)]
    [InlineData("SND-PROFILE-006", 2)]
    [InlineData("SND-API-001", 2)]
    public void A_refused_query_exits_1_and_a_setup_problem_2(string code, int expected) =>
        Assert.Equal(expected, ContextVerbs.ExitCode(new Kernel.Error(code, Kernel.ErrorKind.Invalid, "m")));

    [Fact, Trait("Lane", "unit")]
    public void The_text_names_each_result_its_citation_and_each_source_in_words()
    {
        var answer = new QueryContextResponse("host lease", ["host", "lease"],
            [new ContextHit(1, "SqliteLease.cs", "class SqliteLease", 1.0 / 61, Redacted: false, new Citation(SourceKind.Code, "aw/src/SqliteLease.cs", 12, KnownInstants.Epoch)),
             new ContextHit(2, "deploy.md", "token [REDACTED:github-token]", 1.0 / 62, Redacted: true, new Citation(SourceKind.Notes, "/n/deploy.md", 2, KnownInstants.Epoch))],
            [new SourceReport(SourceKind.Code, SourceState.Ok, 1, Detail: null), new SourceReport(SourceKind.Notes, SourceState.Ok, 1, Detail: null),
             new SourceReport(SourceKind.Linear, SourceState.Failed, 0, "SND-CONTEXT-010 Linear answered HTTP 401")]);

        var text = ContextVerbs.Render(answer);

        Assert.Equal(string.Join('\n',
            "Context for \"host lease\" (terms: host, lease)",
            "DEGRADED: a source didn't answer; results may miss what it holds",
            " 1. [code] aw/src/SqliteLease.cs:12",
            "    class SqliteLease",
            "    retrieved 2026-01-01T00:00:00Z",
            " 2. [notes] /n/deploy.md:2 (redacted)",
            "    token [REDACTED:github-token]",
            "    retrieved 2026-01-01T00:00:00Z",
            "sources: code ok (1 hits); notes ok (1 hits); linear failed (SND-CONTEXT-010 Linear answered HTTP 401)",
            string.Empty), text);
        Assert.DoesNotContain('\u001b', text);
    }
}
