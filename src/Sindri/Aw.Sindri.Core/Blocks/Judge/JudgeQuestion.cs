namespace Aw.Sindri.Core.Blocks.Judge;

/// <summary>
/// One judge decision question as a gate (factory addendum §17.28.2: each decision question is a rung-2 gate block): the
/// facts it reads (required ones are the gate's inputs, so the validator checks them upstream), the decisions that pass,
/// and how its input record is built from the facts.
/// </summary>
/// <param name="Name">The question ('visual-critique'); the gate id is 'gate.judge.&lt;name&gt;'.</param>
/// <param name="Summary">One line on what the gate asks.</param>
/// <param name="Inputs">The required facts.</param>
/// <param name="Passing">The decisions that let the transition go ahead.</param>
/// <param name="Build">Builds the input record from the facts (required ones are present; optional ones may be).</param>
internal sealed record JudgeQuestion(
    string Name,
    string Summary,
    IReadOnlyList<PortSpec> Inputs,
    IReadOnlyList<string> Passing,
    Func<IReadOnlyDictionary<string, string>, JudgeInput> Build);
