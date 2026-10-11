using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Context;

/// <summary>The search's variables.</summary>
/// <param name="Term">The search text.</param>
/// <param name="First">The most issues to return.</param>
/// <param name="Teams">Team keys to search, or null for every team (then not sent).</param>
public sealed record LinearSearchVariables(
    string Term,
    int First,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Teams = null);
