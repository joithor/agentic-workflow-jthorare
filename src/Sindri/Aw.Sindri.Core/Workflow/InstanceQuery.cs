namespace Aw.Sindri.Core.Workflow;

/// <summary>Which instances to list.</summary>
/// <param name="IncludeEnded">Include completed and cancelled instances.</param>
/// <param name="Max">The most to return, newest first.</param>
public sealed record InstanceQuery(bool IncludeEnded, int Max = 100);
