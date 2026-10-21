using Aw.Kernel;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The workspace's changes through git: 'log', 'diff --name-status' and 'diff -U0' of 'since..HEAD', read only.</summary>
internal sealed class GitWorkspaceChanges : IWorkspaceChanges
{
    public ValueTask<Result<WorkspaceChanges>> Since(string workspace, string since, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<string>>> BranchFiles(string workspace, string tip, CancellationToken cancellationToken) => throw new NotSupportedException();
}
