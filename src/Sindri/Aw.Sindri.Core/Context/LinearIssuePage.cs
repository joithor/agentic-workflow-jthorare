namespace Aw.Sindri.Core.Context;

/// <summary>A page of issues.</summary>
/// <param name="Nodes">The issues, in Linear's rank order.</param>
public sealed record LinearIssuePage(IReadOnlyList<LinearIssue> Nodes);
