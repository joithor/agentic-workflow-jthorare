namespace Aw.Sindri.Core.Blocks;

/// <summary>A gate's answer.</summary>
/// <param name="Passed">Whether the transition may go ahead.</param>
/// <param name="Detail">Why, in one line.</param>
/// <param name="Score">The score, for judge and model rungs.</param>
/// <param name="Threshold">The threshold the score was held to.</param>
public sealed record GateVerdict(bool Passed, string Detail, double? Score = null, double? Threshold = null);
