using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'gate.pr.secret-scan' (PR babysitter spec §10.1, review finding X1): a push adds no secret shape to a line or a commit message; a hit names the file and line, never the match.</summary>
[Lane(TestLane.Unit)]
public sealed class SecretScanGateTests
{
    private readonly FakeWorkspaceChanges _changes = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<GateResult> Evaluate() => new SecretScanGate(_changes, new FakeSecretScan()).Evaluate(
        PrContexts.Gate("gate.pr.secret-scan", new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = $"\"{PrContexts.Tree}\"", ["state"] = PrContexts.State() }), Token).AsTask();

    private void Added(string message, params AddedLine[] lines) =>
        _changes.Set(PrContexts.Tree, new WorkspaceChanges("c2", [new WorkspaceCommit("c2", message)], [.. lines.Select(static l => l.Path).Distinct(StringComparer.Ordinal).Select(static p => new FileChange(p, FileChangeKind.Modified))], lines));

    [Fact]
    public async Task A_clean_diff_passes_and_scans_from_the_head_the_snapshot_saw()
    {
        Added("fix the build", new AddedLine("src/Foo.cs", 3, "var x = 1;"));

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Equal(PrContexts.Head, _changes.Asked[PrContexts.Tree]);
    }

    [Fact]
    public async Task A_hit_names_the_file_and_line_and_never_the_match()
    {
        Added("fix the build", new AddedLine("src/Foo.cs", 3, "var x = 1;"), new AddedLine("src/Foo.cs", 4, $"var token = \"{FakeSecretScan.Marker}-abc123\";"));

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Equal(["secret-shaped text at src/Foo.cs:4 (remove it and rotate it if it was real)"], result.Reasons);
        Assert.DoesNotContain(result.Reasons, static r => r.Contains(FakeSecretScan.Marker, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_secret_spread_over_lines_of_one_file_is_found_on_the_line_it_starts()
    {
        Added("fix", new AddedLine("keys.pem", 10, "-----BEGIN"), new AddedLine("keys.pem", 11, FakeSecretScan.Marker), new AddedLine("keys.pem", 12, "-----END"));

        var result = await Evaluate();

        Assert.Equal(["secret-shaped text at keys.pem:11 (remove it and rotate it if it was real)"], result.Reasons);
    }

    [Fact]
    public async Task A_hit_in_a_commit_message_names_the_commit_not_the_text()
    {
        Added($"use {FakeSecretScan.Marker} to log in", new AddedLine("src/Foo.cs", 3, "var x = 1;"));

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Equal(["secret-shaped text in the message of commit c2"], result.Reasons);
    }

    [Fact]
    public async Task Two_files_with_hits_give_one_reason_each_in_path_order()
    {
        Added("fix", new AddedLine("b.txt", 1, FakeSecretScan.Marker), new AddedLine("a.txt", 2, FakeSecretScan.Marker));

        var result = await Evaluate();

        Assert.Equal(
            ["secret-shaped text at a.txt:2 (remove it and rotate it if it was real)", "secret-shaped text at b.txt:1 (remove it and rotate it if it was real)"],
            result.Reasons);
    }

    [Fact]
    public async Task A_workspace_git_cannot_read_fails_closed()
    {
        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains(PrContexts.Tree, StringComparison.Ordinal));
    }
}
