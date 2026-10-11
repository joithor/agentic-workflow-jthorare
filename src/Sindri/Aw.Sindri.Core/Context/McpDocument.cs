namespace Aw.Sindri.Core.Context;

/// <summary>A document an MCP search tool returned.</summary>
/// <param name="Uri">Its permalink.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its text, or an excerpt of it.</param>
public sealed record McpDocument(string Uri, string Title, string Text);
