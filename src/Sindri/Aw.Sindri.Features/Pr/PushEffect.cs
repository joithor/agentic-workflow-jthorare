using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.Pr;

/// <summary>'effect.pr.push' (PR babysitter spec §10.1, cause 'ci' only): pushes the workspace's HEAD to the pull request's head branch with a lease on the head the snapshot saw. Replies 'pr.pushed'.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="changes">The commits and files of the push.</param>
/// <param name="diff">Reads the workspace's HEAD.</param>
/// <param name="contents">Reads the facts the effect names.</param>
/// <param name="store">Stores 'fact:push'.</param>
internal sealed class PushEffect(IPullRequests pulls, IWorkspaceChanges changes, IRunDiff diff, IFactContents contents, IFactStore store) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.pr.push"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects | Capability.Network, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(store);
        throw new NotSupportedException();
    }
}
