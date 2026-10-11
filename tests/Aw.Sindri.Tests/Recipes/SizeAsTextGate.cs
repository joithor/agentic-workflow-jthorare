using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>A gate that reads triage.size as text, to prove the validator matches types, not just names.</summary>
internal sealed class SizeAsTextGate : IGateBlock
{
    public BlockSpec Spec { get; } = new(BlockId.From("gate.test.size"), BlockKind.Gate, "test", [new("triage.size", PortType.Text)], [], Rung: GateRung.Deterministic);

    public ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<GateVerdict>>(new GateVerdict(Passed: true, "ok"));
}
