using System.Text;

namespace Aw.Connectors.Core.Slack;

/// <summary>
/// Where a Slack read stands: the newest ts seen per channel (ConnectAI's per-channel watermark map). Text form 'C0123=1700000000.000100,C0456=…', sorted by channel id.
/// A channel not in the map has not been read. Immutable.
/// </summary>
public sealed class SlackCursor
{
    private readonly Dictionary<string, string> _newest;

    private SlackCursor(Dictionary<string, string> newest)
    {
        _newest = newest;
    }

    /// <summary>A cursor that has read nothing.</summary>
    public static SlackCursor Empty { get; } = new([]);

    /// <summary>The watermark of <paramref name="channel"/>, or null when it has not been read.</summary>
    /// <param name="channel">The channel id.</param>
    /// <returns>The newest ts seen.</returns>
    public string? Newest(string channel) => _newest.TryGetValue(channel, out var ts) ? ts : null;

    /// <summary>The cursor with <paramref name="channel"/> moved to <paramref name="ts"/> (never backwards).</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="ts">The newest ts read in that channel.</param>
    /// <returns>The new cursor.</returns>
    public SlackCursor With(string channel, string ts)
    {
        var next = new Dictionary<string, string>(_newest, StringComparer.Ordinal)
        {
            [channel] = _newest.TryGetValue(channel, out var old) ? SlackTs.Later(old, ts) : ts,
        };
        return new SlackCursor(next);
    }

    /// <summary>The text form the Host stores.</summary>
    /// <returns>The text.</returns>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var (channel, ts) in _newest.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            _ = text.Append(text.Length == 0 ? string.Empty : ",").Append(channel).Append('=').Append(ts);
        }

        return text.ToString();
    }

    /// <summary>Reads the text form.</summary>
    /// <param name="text">What <see cref="ToText"/> made, or null for a first run.</param>
    /// <returns>The cursor, or null when the text is not a cursor (the caller starts over and says the cursor was reset).</returns>
    public static SlackCursor? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Empty;
        }

        Dictionary<string, string> newest = new(StringComparer.Ordinal);
        foreach (var part in text.Split(','))
        {
            var pair = part.Split('=');
            if (pair is not [var channel, var ts] || !SlackChannelIds.IsReadable(channel) || !SlackTs.TryParse(ts, out _))
            {
                return null;
            }

            newest[channel] = ts;
        }

        return new SlackCursor(newest);
    }
}
