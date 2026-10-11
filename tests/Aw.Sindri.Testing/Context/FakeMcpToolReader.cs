using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;

namespace Aw.Sindri.Testing.Context;

/// <summary>An in-memory MCP search tool over seeded documents; passes <see cref="McpToolReaderContract"/>.</summary>
/// <param name="documents">The seed.</param>
public sealed class FakeMcpToolReader(IReadOnlyList<SeedDocument> documents) : IMcpToolReader
{
    /// <summary>The calls it received.</summary>
    public IList<McpToolCall> Calls { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<McpDocument>>> Call(McpToolCall call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<McpDocument>>(SindriErrors.ContextSourceFailed.With("the MCP call was cancelled")));
        }

        var terms = call.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<McpDocument> found =
            [.. FakeSearch.Run(documents, terms, call.Limit).Select(m => new McpDocument("mcp://fake/" + m.Document.Name, m.Document.Title, m.Document.Body))];
        return ValueTask.FromResult(Results.Success(found));
    }
}
