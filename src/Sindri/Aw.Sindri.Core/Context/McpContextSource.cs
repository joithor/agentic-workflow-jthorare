using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Context;

/// <summary>The ConnectAI source: its search tool's documents as hits, in the tool's order.</summary>
/// <param name="reader">The MCP tool reader.</param>
/// <param name="settings">The server and tool the profile names.</param>
public sealed class McpContextSource(IMcpToolReader reader, McpContext settings) : IContextSource
{
    /// <inheritdoc/>
    public SourceKind Kind => SourceKind.ConnectAi;

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        var call = new McpToolCall(settings.Server, settings.Tool, string.Join(' ', query.Terms), query.Limit);
        var documents = await reader.Call(call, cancellationToken).ConfigureAwait(false);
        if (documents.IsError)
        {
            return documents.Error;
        }

        return Results.Success<IReadOnlyList<SourceHit>>(
        [
            .. documents.Value.Take(query.Limit).Select(d => new SourceHit(d.Uri, Line: null, Snippets.Raw(d.Title), Snippets.Around(d.Text, query.Terms))),
        ]);
    }
}
