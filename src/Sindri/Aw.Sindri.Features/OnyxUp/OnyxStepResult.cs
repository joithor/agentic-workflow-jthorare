namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What one step of `onyx up` did in this run.</summary>
/// <param name="Step">The step's name (fetch, config, pull, start).</param>
/// <param name="Outcome">What it did.</param>
/// <param name="Hint">For a step that needs the human: what to do; null otherwise.</param>
public sealed record OnyxStepResult(string Step, OnyxStepOutcome Outcome, string? Hint = null);
