using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>An MCP-backed source's settings: the server and the read-only search tool it calls.</summary>
/// <param name="Server">The MCP server's name.</param>
/// <param name="Tool">The read-only search tool on it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record McpContext(string Server, string Tool);
