using Aw.Kernel;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'effect.pr.rerun-checks' (PR babysitter spec §10.1): asks the forge to run the failed checks of the snapshot's head again. Replies 'pr.rerun'.</summary>
/// <param name="pulls">The forge.</param>
/// <param name="contents">Reads the facts the effect names.</param>
internal sealed class RerunChecksEffect(IPullRequests pulls, IFactContents contents) : IEffectKind, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From("effect.pr.rerun-checks"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
        Capability.Effects | Capability.Network, Parameters: [], Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(contents);
        throw new NotSupportedException();
    }
}
