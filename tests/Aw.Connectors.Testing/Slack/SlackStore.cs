using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>The messages a fake Slack holds, and how Slack reads them: history is the top-level messages newest first, a thread is its root then its replies oldest first.</summary>
internal sealed class SlackStore
{
    private readonly Dictionary<string, List<SlackMessage>> _channels = new(StringComparer.Ordinal);

    public void Add(string channelId, SlackMessage message)
    {
        if (!_channels.TryGetValue(channelId, out var list))
        {
            list = [];
            _channels[channelId] = list;
        }

        list.Add(message);
    }

    /// <summary>Top-level messages with ts at or after <paramref name="oldest"/>, newest first; a root carries the reply count the store holds and its own ts as thread_ts.</summary>
    public List<SlackMessage> Top(string channelId, string? oldest)
    {
        var all = All(channelId);
        return [.. all
            .Where(m => (m.ThreadTs is null || m.ThreadTs == m.Ts) && (oldest is null || string.CompareOrdinal(Pad(m.Ts), Pad(oldest)) >= 0))
            .OrderByDescending(static m => Pad(m.Ts), StringComparer.Ordinal)
            .Select(m =>
            {
                var replies = all.Count(r => r.ThreadTs == m.Ts && r.Ts != m.Ts);
                return replies == 0 ? m : m with { ThreadTs = m.Ts, ReplyCount = replies };
            })];
    }

    /// <summary>The root and its replies, oldest first.</summary>
    public List<SlackMessage> Thread(string channelId, string threadTs)
        => [.. All(channelId).Where(m => m.Ts == threadTs || m.ThreadTs == threadTs).OrderBy(static m => Pad(m.Ts), StringComparer.Ordinal)];

    private List<SlackMessage> All(string channelId) => _channels.TryGetValue(channelId, out var list) ? list : [];

    private static string Pad(string ts) => ts.PadLeft(20, '0');
}
