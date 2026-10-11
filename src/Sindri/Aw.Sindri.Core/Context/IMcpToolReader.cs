using Aw.Kernel;

namespace Aw.Sindri.Core.Context;

/// <summary>
/// Calls a read-only search tool on an MCP server (port; factory addendum §17.3: MCP servers count as connectors through
/// a read-only client). The ConnectAI source reads through it. This build ships the port and its fake; the real
/// client comes when ConnectAI's read surface is wired. Contract (McpToolReaderContract): documents best first, at most
/// the limit, and SND-CONTEXT-010 instead of an exception when the server can't answer.
/// </summary>
public interface IMcpToolReader
{
    /// <summary>Calls the tool.</summary>
    /// <param name="call">The server, tool, query and limit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The documents, or SND-CONTEXT-010.</returns>
    public ValueTask<Result<IReadOnlyList<McpDocument>>> Call(McpToolCall call, CancellationToken cancellationToken);
}
