namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST …/cancel: why.</summary>
internal sealed record ReasonBody(string? Reason = null);
