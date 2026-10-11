using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>
/// What a block declares (factory addendum §17.28.2): its id and kind, its typed inputs and outputs (so the validator can
/// check a recipe wires them), whether it bears a capability (tools, outward effects or autonomy: only approved recipes
/// may name it, invariant 13), a gate's rung, and an effect's outbox command kind.
/// </summary>
/// <param name="Id">The block id recipes name.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Summary">One line on what it does.</param>
/// <param name="Inputs">The facts it reads, produced upstream.</param>
/// <param name="Outputs">The facts it produces (a step's outputs).</param>
/// <param name="Capability">True when only approved recipes may name it.</param>
/// <param name="Rung">A gate's rung; null for other kinds.</param>
/// <param name="CommandKind">An effect's outbox command kind; null for other kinds.</param>
public sealed record BlockSpec(
    BlockId Id,
    BlockKind Kind,
    string Summary,
    IReadOnlyList<PortSpec> Inputs,
    IReadOnlyList<PortSpec> Outputs,
    bool Capability = false,
    GateRung? Rung = null,
    string? CommandKind = null);
