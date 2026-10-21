using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'gate.pr.diff-scope' with 'scope: phase' (PR babysitter spec §10.1): every changed path is in the phase's allowed files or a test path, no CI config changed, no test deleted or skipped.</summary>
[Lane(TestLane.Unit)]
public sealed class DiffScopeGateTests
{
    private const string PhaseDir = "/packets/p1";

    private readonly FakeWorkspaceChanges _changes = new();
    private readonly InMemoryBriefFiles _files = new InMemoryBriefFiles().With("phase.json", """{"kind":"fix","allowedFiles":["src/**","docs/usage.md","new: src/Fresh.cs"],"testChange":[]}""");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<GateResult> Evaluate(string scope = "phase", string? phase = null, bool packet = true)
    {
        Dictionary<string, string> arguments = new(StringComparer.Ordinal)
        {
            ["scope"] = scope,
            ["workspace"] = $"\"{PrContexts.Tree}\"",
            ["state"] = PrContexts.State(),
        };
        if (packet)
        {
            arguments["phase"] = phase ?? $$"""{"hash":"sha256:aa","dir":"{{PhaseDir}}"}""";
        }

        return new DiffScopeGate(_changes, _files).Evaluate(PrContexts.Gate("gate.pr.diff-scope", arguments), Token).AsTask();
    }

    private void Changed(params string[] files) => _changes.SetCommit(PrContexts.Tree, "fix the build", files);

    [Fact]
    public async Task A_fix_inside_the_allowed_files_passes_and_compares_from_the_head_the_snapshot_saw()
    {
        Changed("src/Foo.cs", "src/Fresh.cs", "docs/usage.md");

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Pass, result.Verdict);
        Assert.Equal(PrContexts.Head, _changes.Asked[PrContexts.Tree]);
    }

    [Fact]
    public async Task A_test_path_passes_even_outside_the_allowed_files()
    {
        Changed("src/Foo.cs", "tests/Aw.Foo.Tests/FooTests.cs");

        Assert.Equal(GateVerdict.Pass, (await Evaluate()).Verdict);
    }

    [Fact]
    public async Task A_path_outside_the_allowed_files_fails_and_names_it()
    {
        Changed("src/Foo.cs", "build/release.sh");

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("build/release.sh", StringComparison.Ordinal) && r.Contains("allowed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_CI_config_change_fails_even_when_every_file_is_allowed()
    {
        _ = _files.With("phase.json", """{"allowedFiles":["**"]}""");
        Changed(".github/workflows/ci.yml");

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains(".github/workflows/ci.yml", StringComparison.Ordinal) && r.Contains("CI config", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_deleted_test_file_fails()
    {
        Changed("src/Foo.cs", "-tests/FooTests.cs");

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("tests/FooTests.cs", StringComparison.Ordinal) && r.Contains("deleted", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[Fact(Skip = \"flaky\")]")]
    [InlineData("    it.skip('adds', () => {")]
    [InlineData("xit('adds', () => {")]
    [InlineData("@Disabled(\"later\")")]
    [InlineData("@pytest.mark.skip(reason='later')")]
    [InlineData("t.Skip(\"later\")")]
    public async Task A_skip_marker_added_to_a_test_fails_naming_the_line(string line)
    {
        _changes.Set(PrContexts.Tree, new WorkspaceChanges("c2", [new WorkspaceCommit("c2", "m")], [new FileChange("tests/FooTests.cs", FileChangeKind.Modified)], [new AddedLine("tests/FooTests.cs", 14, line)]));

        var result = await Evaluate();

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("tests/FooTests.cs:14", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_word_skip_in_source_is_not_a_skipped_test()
    {
        _changes.Set(PrContexts.Tree, new WorkspaceChanges("c2", [new WorkspaceCommit("c2", "m")], [new FileChange("src/Foo.cs", FileChangeKind.Modified)], [new AddedLine("src/Foo.cs", 3, "queue.Skip(1);")]));

        Assert.Equal(GateVerdict.Pass, (await Evaluate()).Verdict);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task With_no_allowed_files_the_pull_requests_own_files_and_tests_are_in_scope(bool packet)
    {
        _ = _files.With("phase.json", """{"allowedFiles":[]}""");
        _changes.SetBranchFiles(PrContexts.Tree, "src/Foo.cs");
        Changed("src/Foo.cs", "tests/FooTests.cs");

        Assert.Equal(GateVerdict.Pass, (await Evaluate(packet: packet)).Verdict);

        Changed("src/Foo.cs", "src/Other.cs");
        var result = await Evaluate(packet: packet);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("src/Other.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_phase_packet_whose_file_cannot_be_read_fails_closed()
    {
        var result = await new DiffScopeGate(_changes, new InMemoryBriefFiles()).Evaluate(PrContexts.Gate("gate.pr.diff-scope", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scope"] = "phase",
            ["workspace"] = $"\"{PrContexts.Tree}\"",
            ["state"] = PrContexts.State(),
            ["phase"] = $$"""{"hash":"sha256:aa","dir":"{{PhaseDir}}"}""",
        }), Token);

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("phase packet", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_workspace_git_cannot_read_fails()
    {
        var result = await Evaluate();   // the fake knows no changes for the workspace

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains(PrContexts.Tree, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_scope_other_than_phase_fails()
    {
        Changed("src/Foo.cs");

        var result = await Evaluate(scope: "docs");

        Assert.Equal(GateVerdict.Fail, result.Verdict);
        Assert.Contains(result.Reasons, static r => r.Contains("scope", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_fix_that_changed_nothing_passes()
    {
        _changes.Set(PrContexts.Tree, new WorkspaceChanges(PrContexts.Head, [], [], []));

        Assert.Equal(GateVerdict.Pass, (await Evaluate()).Verdict);
    }
}
