using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The Linear source's settings. The API key never sits in the profile: it comes from LINEAR_API_KEY.</summary>
/// <param name="Teams">Team keys to search (for example ENG); omitted, every team the key can read.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinearContext(IReadOnlyList<string>? Teams = null);
