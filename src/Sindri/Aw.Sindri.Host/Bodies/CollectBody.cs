using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST …/evidence/collect.</summary>
internal sealed record CollectBody(string Kind, HeadSha Sha, IReadOnlyDictionary<string, string>? Inputs);
