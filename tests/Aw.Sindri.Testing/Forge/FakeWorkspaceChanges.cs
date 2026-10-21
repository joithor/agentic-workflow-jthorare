using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Forge;

/// <summary>An <see cref="IWorkspaceChanges"/> in memory: the changes a test sets per workspace, answered whatever the base.</summary>
public sealed class FakeWorkspaceChanges : IWorkspaceChanges
{
    private readonly Dictionary<string, WorkspaceChanges> _changes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _branches = new(StringComparer.Ordinal);

    /// <summary>The base each <see cref="Since"/> was asked for, per workspace.</summary>
    public IDictionary<string, string> Asked { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Sets what <paramref name="workspace"/> changed.</summary>
    /// <param name="workspace">The path.</param>
    /// <param name="changes">The changes.</param>
    public void Set(string workspace, WorkspaceChanges changes) => _changes[workspace] = changes;

    /// <summary>Sets the files <paramref name="workspace"/>'s branch changed against the default branch.</summary>
    /// <param name="workspace">The path.</param>
    /// <param name="files">The files.</param>
    public void SetBranchFiles(string workspace, params string[] files) => _branches[workspace] = files;

    /// <summary>Sets one commit that changed <paramref name="files"/>, adding no lines.</summary>
    /// <param name="workspace">The path.</param>
    /// <param name="message">The commit's message.</param>
    /// <param name="files">The files it changed; a leading '-' deletes it.</param>
    public void SetCommit(string workspace, string message, params string[] files) => Set(workspace, new WorkspaceChanges(
        "c2", [new WorkspaceCommit("c2", message)],
        [.. files.Select(static f => f.StartsWith('-') ? new FileChange(f[1..], FileChangeKind.Deleted) : new FileChange(f, FileChangeKind.Modified))], []));

    /// <inheritdoc/>
    public ValueTask<Result<WorkspaceChanges>> Since(string workspace, string since, CancellationToken cancellationToken)
    {
        Asked[workspace] = since;
        return ValueTask.FromResult<Result<WorkspaceChanges>>(_changes.TryGetValue(workspace, out var found) ? found : Missing(workspace));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> BranchFiles(string workspace, string tip, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<string>>>(_branches.TryGetValue(workspace, out var found) ? Results.Success(found) : Missing(workspace));

    private static Error Missing(string workspace) => WorkflowErrors.EffectFailed.With($"{workspace} is not a git worktree");
}
