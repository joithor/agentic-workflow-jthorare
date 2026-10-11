namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>'ui-evidence provenance''s document.</summary>
/// <param name="Provenance">Seeded only when a seed row exists and every profile email is on a seed domain.</param>
/// <param name="Reason">Why.</param>
public sealed record DatabaseProvenance(DataProvenance Provenance, string Reason);
