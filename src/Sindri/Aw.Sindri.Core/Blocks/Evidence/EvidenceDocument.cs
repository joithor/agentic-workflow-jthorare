namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>A document a ui-evidence run wrote.</summary>
/// <param name="Passed">Whether every check passed (exit 0; a failed check is exit 2).</param>
/// <param name="Path">The document's absolute path.</param>
/// <param name="Digest">'sha256:&lt;hex&gt;' of its bytes.</param>
public sealed record EvidenceDocument(bool Passed, string Path, string Digest);
