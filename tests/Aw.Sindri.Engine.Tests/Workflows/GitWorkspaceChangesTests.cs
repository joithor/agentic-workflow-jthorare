using Aw.Kernel.Testing;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Tests.Workflows.Effects;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The git adapter of the workspace's changes against a real temp repo: commits, files and added lines of 'since..HEAD', and a branch's files against the default branch.</summary>
[Lane(TestLane.Integration)]
public sealed class GitWorkspaceChangesTests : IDisposable
{
    private static readonly string[] _commit = ["-c", "user.email=test@example.invalid", "-c", "user.name=test", "commit", "-q", "-m"];
    private readonly List<TempGitRepo> _repos = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }
    }

    private async Task<TempGitRepo> Repo()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        Write(repo, "src/Foo.cs", "one\ntwo\nthree\n");
        Write(repo, "tests/FooTests.cs", "test\n");
        Write(repo, "docs/usage.md", "usage\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "base"]);
        return repo;
    }

    private static void Write(TempGitRepo repo, string path, string text)
    {
        var full = Path.Combine(repo.Path, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    [Fact]
    public async Task The_range_lists_its_commits_oldest_first_with_their_whole_messages()
    {
        var repo = await Repo();
        var since = await repo.Git("rev-parse", "HEAD");
        Write(repo, "src/Foo.cs", "one\ntwo\nthree\nfour\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "first fix\n\nSindri-Cause: ci"]);
        Write(repo, "src/Foo.cs", "one\ntwo\nthree\nfour\nfive\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "second fix"]);

        var changes = (await new GitWorkspaceChanges().Since(repo.Path, since, Token)).Value;

        Assert.Equal(["first fix\n\nSindri-Cause: ci", "second fix"], changes.Commits.Select(static c => c.Message), StringComparer.Ordinal);
        Assert.Equal(await repo.Git("rev-parse", "HEAD"), changes.Head);
        Assert.Equal(changes.Head, changes.Commits[^1].Sha);
    }

    [Fact]
    public async Task The_files_say_how_they_changed()
    {
        var repo = await Repo();
        var since = await repo.Git("rev-parse", "HEAD");
        Write(repo, "src/Foo.cs", "one\ntwo\nthree\nfour\n");
        Write(repo, "src/New.cs", "new\n");
        File.Delete(Path.Combine(repo.Path, "tests/FooTests.cs"));
        _ = await repo.Git("mv", "docs/usage.md", "docs/guide.md");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "fix"]);

        var files = (await new GitWorkspaceChanges().Since(repo.Path, since, Token)).Value.Files.ToDictionary(static f => f.Path, static f => f.Kind, StringComparer.Ordinal);

        Assert.Equal(
            new Dictionary<string, FileChangeKind>(StringComparer.Ordinal)
            {
                ["src/Foo.cs"] = FileChangeKind.Modified,
                ["src/New.cs"] = FileChangeKind.Added,
                ["tests/FooTests.cs"] = FileChangeKind.Deleted,
                ["docs/guide.md"] = FileChangeKind.Renamed,
            },
            files);
    }

    [Fact]
    public async Task The_added_lines_carry_their_file_and_their_line_number_in_the_new_file()
    {
        var repo = await Repo();
        var since = await repo.Git("rev-parse", "HEAD");
        Write(repo, "src/Foo.cs", "one\nINSERTED\ntwo\nthree\nlast\n");
        Write(repo, "src/New.cs", "a\nb\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "fix"]);

        var added = (await new GitWorkspaceChanges().Since(repo.Path, since, Token)).Value.AddedLines;

        Assert.Equal(
            [new AddedLine("src/Foo.cs", 2, "INSERTED"), new AddedLine("src/Foo.cs", 5, "last"), new AddedLine("src/New.cs", 1, "a"), new AddedLine("src/New.cs", 2, "b")],
            added);
    }

    [Fact]
    public async Task A_workspace_still_at_the_base_has_no_change()
    {
        var repo = await Repo();
        var head = await repo.Git("rev-parse", "HEAD");

        var changes = (await new GitWorkspaceChanges().Since(repo.Path, head, Token)).Value;

        Assert.Empty(changes.Commits);
        Assert.Empty(changes.Files);
        Assert.Empty(changes.AddedLines);
    }

    [Fact]
    public async Task A_base_that_is_not_a_commit_and_a_path_that_is_not_a_worktree_are_errors()
    {
        var repo = await Repo();

        Assert.True((await new GitWorkspaceChanges().Since(repo.Path, "not-a-commit", Token)).IsError);
        Assert.True((await new GitWorkspaceChanges().Since(Path.Combine(repo.Path, "nowhere"), "HEAD", Token)).IsError);
    }

    [Fact]
    public async Task The_branch_files_are_what_the_branch_changed_against_the_default_branch()
    {
        var repo = await Repo();
        _ = await repo.Git("switch", "-q", "-c", "feature");
        Write(repo, "src/Foo.cs", "one\ntwo\nthree\nfour\n");
        Write(repo, "tests/FooTests.cs", "test2\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "feature"]);
        var tip = await repo.Git("rev-parse", "HEAD");
        Write(repo, "src/Later.cs", "later\n");
        _ = await repo.Git("add", "-A");
        _ = await repo.Git([.. _commit, "later"]);

        var files = (await new GitWorkspaceChanges().BranchFiles(repo.Path, tip, Token)).Value;

        Assert.Equal(["src/Foo.cs", "tests/FooTests.cs"], files.Order(StringComparer.Ordinal), StringComparer.Ordinal);   // not src/Later.cs: it is above the tip
    }

    [Fact]
    public async Task A_repo_with_no_default_branch_to_compare_with_is_an_error()
    {
        var repo = await Repo();
        _ = await repo.Git("branch", "-m", "main", "trunk");

        Assert.True((await new GitWorkspaceChanges().BranchFiles(repo.Path, "HEAD", Token)).IsError);
    }
}
