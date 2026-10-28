using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>Reads the listed channels' threads and messages newer than a cursor.</summary>
/// <param name="Channels">The channels to read (id and display name), in the order they are read.</param>
/// <param name="Workspace">The workspace name the permalinks are built from.</param>
/// <param name="After">Where each channel starts; <see cref="SlackCursor.Empty"/> for a first run.</param>
/// <param name="Limit">Stop starting new channels once this many records are read (a channel is read whole).</param>
internal sealed record PullSlackRequest(IReadOnlyList<SlackChannelRef> Channels, string Workspace, SlackCursor After, int Limit)
{
    /// <summary>Gets where a channel with no watermark starts (the index window, D12); null reads the whole channel.</summary>
    public DateTimeOffset? WindowStart { get; init; }
}
