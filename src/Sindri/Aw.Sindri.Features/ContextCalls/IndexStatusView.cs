namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The answer to GET /sindri/index/status: one row per record feed of the approved profile, in source order (none without a feed).</summary>
/// <param name="Sources">The rows.</param>
public sealed record IndexStatusView(IReadOnlyList<IndexSourceRow> Sources);
