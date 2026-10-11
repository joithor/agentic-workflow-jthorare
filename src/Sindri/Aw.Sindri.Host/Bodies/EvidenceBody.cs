using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST …/evidence.</summary>
internal sealed record EvidenceBody(string Kind, HeadSha Sha, string Digest);
