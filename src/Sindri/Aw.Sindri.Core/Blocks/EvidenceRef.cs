using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>A piece of evidence registered for an instance (§17.11): its kind, the sha it is bound to and its content digest.</summary>
/// <param name="Kind">The evidence kind, for example 'test' (block 'evidence.test').</param>
/// <param name="Sha">The commit it was produced on.</param>
/// <param name="Digest">The artifact's digest, so a changed artifact no longer counts.</param>
/// <param name="RegisteredAt">When it was registered.</param>
public sealed record EvidenceRef(string Kind, HeadSha Sha, string Digest, DateTimeOffset RegisteredAt);
