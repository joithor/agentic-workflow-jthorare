using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks;

/// <summary>A gate: evaluated before a transition is applied; its rung, score, threshold and detail are recorded with it.</summary>
public interface IGateBlock : IBlock
{
    /// <summary>Evaluates the gate for one transition.</summary>
    /// <param name="context">The instance's facts, evidence and the transition's sha.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The verdict, or an error when the gate couldn't be evaluated (the transition is refused).</returns>
    public ValueTask<Result<GateVerdict>> Evaluate(GateContext context, CancellationToken cancellationToken);
}
