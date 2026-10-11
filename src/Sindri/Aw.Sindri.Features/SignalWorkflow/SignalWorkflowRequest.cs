using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SignalWorkflow;

/// <summary>Signal an instance: what its current step reports, asking for the transition on <see cref="On"/>.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="On">The signal, for example 'passed'.</param>
/// <param name="ExpectedVersion">The version the caller saw; null takes the current one. A retry with the same version is a no-op.</param>
/// <param name="Sha">The head sha the transition is bound to.</param>
/// <param name="Outputs">The current step's outputs.</param>
/// <param name="Findings">A loop round's open findings.</param>
/// <param name="EmptyDiff">True when a loop round produced an empty diff.</param>
/// <param name="Tokens">Tokens spent since the last signal.</param>
/// <param name="Turns">Turns spent since the last signal.</param>
public sealed record SignalWorkflowRequest(
    InstanceId Instance,
    string On,
    long? ExpectedVersion = null,
    HeadSha? Sha = null,
    IReadOnlyDictionary<string, string>? Outputs = null,
    IReadOnlyList<string>? Findings = null,
    bool EmptyDiff = false,
    long Tokens = 0,
    int Turns = 0);
