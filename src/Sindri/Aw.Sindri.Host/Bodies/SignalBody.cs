using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST /sindri/workflows/{instance}/signal: a SignalWorkflowRequest without the instance (it is in the route).</summary>
internal sealed record SignalBody(
    string On,
    long? ExpectedVersion = null,
    HeadSha? Sha = null,
    IReadOnlyDictionary<string, string>? Outputs = null,
    IReadOnlyList<string>? Findings = null,
    bool EmptyDiff = false,
    long Tokens = 0,
    int Turns = 0);
