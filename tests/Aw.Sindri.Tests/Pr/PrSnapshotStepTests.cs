using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'step.pr.snapshot' on the block runtime: one read of the pull request, the first outcome that holds, and 'fact:prState' with the head and the failing checks.</summary>
[Lane(TestLane.Unit)]
public sealed class PrSnapshotStepTests
{
    private readonly FakePullRequests _pulls = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    private Task<BlockStepResult> Run(Dictionary<string, string>? facts = null) =>
        new PrSnapshotStep(_pulls).Run(PrContexts.Step("step.pr.snapshot", facts ?? new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = $"\"{PrContexts.Link}\"" }), Token).AsTask();

    [Fact]
    public async Task A_red_head_reports_ciRed_and_records_the_head_the_failing_checks_and_their_sha()
    {
        _pulls.Add(PrContexts.Pull(failing: ["lint", "build"]));

        var result = await Run();

        Assert.Equal("ciRed", result.Result!.Outcome.Value);
        var state = Out(result, "prState");
        Assert.Equal(PrContexts.Head, (string?)state["headSha"]);
        Assert.Equal(PrContexts.Head, (string?)state["checksSha"]);
        Assert.Equal(["build", "lint"], state["failing"]!.AsArray().Select(static n => (string?)n), StringComparer.Ordinal);
        Assert.Equal("ciRed", (string?)state["outcome"]);
    }

    [Fact]
    public async Task A_red_head_records_one_finding_per_failing_check_so_the_same_failure_is_no_progress()
    {
        _pulls.Add(PrContexts.Pull(failing: ["lint", "build"]));

        var findings = Out(await Run(), "findings").AsArray();

        Assert.Equal(["build", "lint"], findings.Select(static f => (string?)f!["check"]), StringComparer.Ordinal);
        Assert.All(findings, static f => Assert.Equal(string.Empty, (string?)f!["file"]));
    }

    [Fact]
    public async Task Checks_on_an_older_sha_report_pending_and_record_both_shas()
    {
        _pulls.Add(PrContexts.Pull(PullChecksState.Failing, checksSha: "0ld0ld00"));

        var result = await Run();

        Assert.Equal("pending", result.Result!.Outcome.Value);
        Assert.Equal("0ld0ld00", (string?)Out(result, "prState")["checksSha"]);
        Assert.False(result.Result.Outputs.ContainsKey(FactKey.From("fact:findings")));
    }

    [Fact]
    public async Task A_green_head_records_only_the_state()
    {
        _pulls.Add(PrContexts.Pull(PullChecksState.Passing));

        var result = await Run();

        Assert.Equal("green", result.Result!.Outcome.Value);
        Assert.Equal(["fact:prState"], result.Result.Outputs.Keys.Select(static k => k.Value));
    }

    [Fact]
    public async Task A_merged_pull_request_records_the_merge_commit()
    {
        _pulls.Add(PrContexts.Pull() with { State = PullState.Merged, MergedSha = "9f2c1ab" });

        var result = await Run();

        Assert.Equal("merged", result.Result!.Outcome.Value);
        Assert.Equal("9f2c1ab", (string?)Out(result, "mergedSha"));
    }

    [Fact]
    public async Task A_missing_pull_request_link_fails_the_Step()
    {
        var result = await Run(new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Null(result.Result);
        Assert.Contains("fact:pr", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_forge_that_cannot_be_read_fails_the_Step_with_its_reason()
    {
        var result = await Run();   // the fake knows no pull request

        Assert.Null(result.Result);
        Assert.Contains(PrContexts.Link, result.Failure, StringComparison.Ordinal);
    }
}
