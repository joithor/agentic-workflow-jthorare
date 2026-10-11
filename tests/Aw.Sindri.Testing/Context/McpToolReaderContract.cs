using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Xunit;

namespace Aw.Sindri.Testing.Context;

/// <summary>The contract every <see cref="IMcpToolReader"/> honours: matching documents best first, at most the limit, SND-CONTEXT-010 when it can't answer.</summary>
public abstract class McpToolReaderContract
{
    private static readonly IReadOnlyList<SeedDocument> _corpus =
    [
        new("decision-1", "Lease decision", "We fence every write by the lease epoch."),
        new("decision-2", "Lease renewal", "Renew the lease every twenty seconds."),
        new("other", "Lunch", "Pizza on Fridays."),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The reader under test, its search tool seeded with <paramref name="documents"/>.</summary>
    /// <param name="documents">The documents.</param>
    /// <returns>The reader.</returns>
    protected abstract Task<IMcpToolReader> Create(IReadOnlyList<SeedDocument> documents);

    /// <summary>The tool returns the matching documents, best first, within the limit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_tool_returns_matching_documents_within_the_limit()
    {
        var reader = await Create(_corpus);

        var all = (await reader.Call(new McpToolCall("connectai", "search", "lease epoch", 10), Token)).Value;
        var one = (await reader.Call(new McpToolCall("connectai", "search", "lease epoch", 1), Token)).Value;

        Expect.Equal(2, all.Count);
        Expect.Equal("Lease decision", all[0].Title);
        Expect.Equal(1, one.Count);
        Expect.True(all.All(d => d.Uri.Length > 0));
    }

    /// <summary>A cancelled call is SND-CONTEXT-010.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_cancelled_call_is_a_failed_source()
    {
        var reader = await Create(_corpus);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var answer = await reader.Call(new McpToolCall("connectai", "search", "lease", 10), cancelled.Token);

        Expect.Equal(SindriErrors.ContextSourceFailed.Code, answer.Error.Code);
    }
}
