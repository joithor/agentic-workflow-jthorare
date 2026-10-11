using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The context retrieval engine's settings (factory addendum §17.29.2, docs/csharp/sindri-context.md): which sources are
/// on (a source is on when its key is present) and how many results a query returns. An optional key of schema version 1.
/// </summary>
/// <param name="Code">The code source: the profile repos' tracked files.</param>
/// <param name="Notes">The notes source: a directory of Markdown and text files.</param>
/// <param name="Linear">The Linear source: issue search, read-only, with the key from the environment.</param>
/// <param name="ConnectAi">The ConnectAI source, read through an MCP tool (a port in this build).</param>
/// <param name="Limit">How many results a query returns unless it asks for fewer or more (1 to 50).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ContextSettings(
    CodeContext? Code = null,
    NotesContext? Notes = null,
    LinearContext? Linear = null,
    [property: JsonPropertyName("connectai")] McpContext? ConnectAi = null,
    int Limit = ContextSettings.DefaultLimit)
{
    /// <summary>The default result count.</summary>
    public const int DefaultLimit = 10;

    /// <summary>The largest result count a profile or a query may ask for.</summary>
    public const int MaxLimit = 50;
}
