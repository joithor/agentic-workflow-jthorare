using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A gate's evaluation, recorded with the transition (§17.28.1): its rung, verdict, score and threshold.</summary>
/// <param name="Gate">The gate block.</param>
/// <param name="Rung">Its rung.</param>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Detail">Its detail.</param>
/// <param name="Score">Its score, if any.</param>
/// <param name="Threshold">Its threshold, if any.</param>
public sealed record GateRecord(BlockId Gate, GateRung Rung, bool Passed, string Detail, double? Score = null, double? Threshold = null);
