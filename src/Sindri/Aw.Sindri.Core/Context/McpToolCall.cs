namespace Aw.Sindri.Core.Context;

/// <summary>A call to a read-only MCP search tool.</summary>
/// <param name="Server">The MCP server.</param>
/// <param name="Tool">The tool.</param>
/// <param name="Query">The search text.</param>
/// <param name="Limit">The most documents to return.</param>
public sealed record McpToolCall(string Server, string Tool, string Query, int Limit);
