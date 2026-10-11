using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>A gate whose verdict a test sets, on any rung; it counts its calls and keeps the last context it saw.</summary>
internal sealed class TestGate(string id, GateRung rung) : IGateBlock
{
    public BlockSpec Spec { get; } = new(BlockId.From(id), BlockKind.Gate, "test gate", [], [], Rung: rung);

    public bool Passes { get; set; }

    public int Calls { get; private set; }

    public GateContext? Seen { get; private set; }

    public ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        Calls++;
        Seen = context;
        return ValueTask.FromResult<Result<GateVerdict>>(new GateVerdict(Passes, Passes ? "ok" : "below threshold", Score: Passes ? 0.9 : 0.2, Threshold: 0.7));
    }
}
