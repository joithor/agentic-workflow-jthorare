using Aw.Sindri.Core.Context;
using Aw.Sindri.Infrastructure.Context;

namespace Aw.Sindri.Tests.Context;

/// <summary>The file-backed sources' own rules beyond the contract: symlinks, oversized files, partly broken repo lists.</summary>
public sealed class LineSourceTests : IDisposable
{
    private readonly TempTree _tree = new();
    private readonly TempTree _outside = new();

    public void Dispose()
    {
        _tree.Dispose();
        _outside.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_notes_walk_skips_symlinks_and_files_over_a_megabyte()
    {
        _ = _tree.Write("notes/in.md", "lease inside");
        _ = _tree.Write("notes/big.md", "lease " + new string('x', 1024 * 1024));
        _ = _outside.Write("away.md", "lease outside");
        _ = File.CreateSymbolicLink(Path.Combine(_tree.Path, "notes", "linked"), _outside.Path);
        _ = File.CreateSymbolicLink(Path.Combine(_tree.Path, "notes", "file-link.md"), Path.Combine(_outside.Path, "away.md"));
        _ = File.CreateSymbolicLink(Path.Combine(_tree.Path, "notes", "loop"), Path.Combine(_tree.Path, "notes"));

        var hits = (await new NotesDirectorySource(Path.Combine(_tree.Path, "notes")).Search(new SourceQuery(["lease"], 10), Token)).Value;

        Assert.Equal([Path.Combine(_tree.Path, "notes", "in.md")], hits.Select(h => h.Document), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_that_is_not_a_checkout_leaves_the_others_answering()
    {
        _ = _tree.Write("src/lease.cs", "// lease");
        _tree.GitAddAll();
        var source = new GitGrepCodeSource([new CodeRepo("good", _tree.Path), new CodeRepo("plain", _outside.Path)]);

        var hits = (await source.Search(new SourceQuery(["lease"], 10), Token)).Value;
        var allBad = await new GitGrepCodeSource([new CodeRepo("plain", _outside.Path)]).Search(new SourceQuery(["lease"], 10), Token);

        Assert.Equal(["good/src/lease.cs"], hits.Select(h => h.Document), StringComparer.Ordinal);
        Assert.Contains("not a git repository", allBad.Error.Message, StringComparison.Ordinal);
    }
}
