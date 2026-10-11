using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The code source's settings.</summary>
/// <param name="Repos">The profile repos to search, by name; omitted, every profile repo.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CodeContext(IReadOnlyList<string>? Repos = null);
