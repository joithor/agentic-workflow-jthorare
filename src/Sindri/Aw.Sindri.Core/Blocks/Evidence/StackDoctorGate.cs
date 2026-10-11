using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>'gate.evidence.doctor' (rung 1): passes when 'ui-evidence doctor' says the local stack is healthy; the failures are the detail.</summary>
/// <param name="tool">ui-evidence.</param>
internal sealed class StackDoctorGate(IUiEvidence tool) : IGateBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = new(BlockId.From("gate.evidence.doctor"), BlockKind.Gate, "ui-evidence doctor: the local stack is up and is this app", [], [],
        Rung: GateRung.Deterministic);

    /// <inheritdoc/>
    public async ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        var health = await tool.Doctor(cancellationToken).ConfigureAwait(false);
        if (health.IsError)
        {
            return health.Error;
        }

        return health.Value.Healthy
            ? new GateVerdict(Passed: true, "ui-evidence doctor: the stack is healthy")
            : new GateVerdict(Passed: false, $"ui-evidence doctor: {string.Join("; ", health.Value.Failures)}");
    }
}
