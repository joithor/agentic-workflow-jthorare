using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The notes source's settings.</summary>
/// <param name="Path">The notes directory: an absolute path, or one starting with ~/.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NotesContext(string Path);
