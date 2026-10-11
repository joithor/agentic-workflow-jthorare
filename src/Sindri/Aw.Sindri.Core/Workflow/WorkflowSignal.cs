using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A signal asking an instance to take a transition: what the current step reports.</summary>
/// <param name="On">The signal, for example 'passed'.</param>
/// <param name="ExpectedVersion">The version the caller saw; null takes the current one. A retry with the same version is a no-op.</param>
/// <param name="Sha">The head sha the transition is bound to, if any.</param>
/// <param name="Outputs">The current step's outputs, by name (only the outputs its block declares, each of its type).</param>
/// <param name="Findings">For a loop round: its open findings, keyed by file, check and normalized message (§17.23.13).</param>
/// <param name="EmptyDiff">For a loop round: true when the round produced an empty diff.</param>
/// <param name="Spend">Tokens and turns spent since the last signal.</param>
public sealed record WorkflowSignal(
    string On,
    long? ExpectedVersion,
    HeadSha? Sha,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string> Findings,
    bool EmptyDiff,
    Spend Spend);
