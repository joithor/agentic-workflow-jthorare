using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.Prepare;

/// <summary>The host-side prepare of a Step's worktree (sandbox spec §3.8, SB-11).</summary>
/// <param name="profile">The approved profile, for the repo's <c lang="text">sandbox.repos.&lt;repo&gt;.prepare</c>.</param>
/// <param name="source">Reads the lockfiles at the base commit.</param>
/// <param name="cache">The installs kept per lockfile hash.</param>
/// <param name="commands">Runs the install.</param>
/// <param name="slots">The box-wide heavy-job lock.</param>
/// <param name="clock">Times the prepare's limit.</param>
internal sealed class WorkspacePrepare(IApprovedProfile profile, IPrepareSource source, IPrepareCache cache, ICheckCommands commands, IHeavySlots slots, TimeProvider clock) : IWorkspacePrepare
{
    /// <summary>The name of the install command, as <see cref="ICheckCommands"/> is told it.</summary>
    internal const string InstallName = "prepare-install";

    /// <summary>The name of the rebuild command that runs the allowed install scripts.</summary>
    internal const string RebuildName = "prepare-rebuild";

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Prepare(LaunchRequest request, string workDirectory, IProgress<string>? progress, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(Unit.Value));
}
