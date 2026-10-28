using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>
/// Reads each listed channel whole, newest first, from its watermark's day (so a day record is always rebuilt from its first message) or the window start, folds the threads (one
/// replies read per root with replies) and the day buckets into records, and moves that channel's watermark to the newest message seen. A channel is committed only when it was read whole:
/// a rate limit in the middle of one drops that channel's records and leaves its cursor where it was, so it comes again complete; the channels before it keep theirs.
/// </summary>
internal sealed class PullSlackService(ISlackReader reader) : IService<PullSlackRequest, PullSlackResponse>
{
    public async ValueTask<Result<PullSlackResponse>> Handle(PullSlackRequest request, CancellationToken cancellationToken)
    {
        List<ConnectorItem> items = [];
        var next = request.After;
        foreach (var channel in request.Channels)
        {
            if (items.Count >= request.Limit)
            {
                break;
            }

            var read = await ReadChannel(request, channel, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.RateLimitedUntil is { } until)
            {
                return new PullSlackResponse(items, next, until);
            }

            items.AddRange(read.Value.Items);
            next = read.Value.Newest is { } newest ? next.With(channel.Channel, newest) : next;
        }

        return new PullSlackResponse(items, next, RateLimitedUntil: null);
    }

    private static string? Oldest(PullSlackRequest request, SlackChannelRef channel)
        => request.After.Newest(channel.Channel) is { } watermark
            ? SlackTs.DayStart(watermark)
            : request.WindowStart is { } window ? SlackTs.Format(window) : null;

    // why: a bounded read starts at the watermark's day, so it gets that day again; with no root newer than the watermark the channel was already read whole up to the bound, and Backfill must see an empty page.
    private static bool ReadWholeUpToBound(PullSlackRequest request, SlackChannelRef channel, string? newest)
        => request.Before is not null && request.After.Newest(channel.Channel) is { } mark && (newest is null || SlackTs.Later(mark, newest) == mark);

    private async ValueTask<Result<ChannelRead>> ReadChannel(PullSlackRequest request, SlackChannelRef channel, CancellationToken cancellationToken)
    {
        var history = await reader.History(channel.Channel, Oldest(request, channel), Latest(request), cancellationToken).ConfigureAwait(false);
        if (history.IsError)
        {
            return history.Error;
        }

        if (history.Value.RateLimitedUntil is { } until)
        {
            return new ChannelRead([], Newest: null, until);
        }

        var roots = history.Value.Items;
        var newest = roots.Select(static m => m.Ts).Aggregate((string?)null, static (a, b) => a is null ? b : SlackTs.Later(a, b));
        if (ReadWholeUpToBound(request, channel, newest))
        {
            return new ChannelRead([], Newest: null, RateLimitedUntil: null);
        }

        return await ReadThreads(request, channel, roots, newest, cancellationToken).ConfigureAwait(false);
    }

    private static string? Latest(PullSlackRequest request) => request.Before is { } end ? SlackTs.Format(end) : null;

    private async ValueTask<Result<ChannelRead>> ReadThreads(PullSlackRequest request, SlackChannelRef channel, IReadOnlyList<SlackMessage> roots, string? newest, CancellationToken cancellationToken)
    {
        List<ConnectorItem> items = [];
        foreach (var root in roots.Where(static m => !SlackRecords.IsNoise(m) && SlackRecords.IsThreadRoot(m)))
        {
            var thread = await reader.Replies(channel.Channel, root.Ts, cancellationToken).ConfigureAwait(false);
            if (thread.IsError)
            {
                return thread.Error;
            }

            if (thread.Value.RateLimitedUntil is { } limited)
            {
                return new ChannelRead([], Newest: null, limited);
            }

            if (thread.Value.Items.Count > 0 && SlackRecords.Thread(channel, request.Workspace, thread.Value.Items) is { } item)
            {
                items.Add(item);
            }
        }

        items.AddRange(Days(channel, request.Workspace, roots));
        return new ChannelRead([.. items.OrderBy(static i => i.CreatedAt).ThenBy(static i => i.Key, StringComparer.Ordinal)], newest, RateLimitedUntil: null);
    }

    private static IEnumerable<ConnectorItem> Days(SlackChannelRef channel, string workspace, IReadOnlyList<SlackMessage> roots)
        => roots
            .Where(static m => !SlackRecords.IsNoise(m) && SlackRecords.IsUnthreaded(m) && SlackRecords.DayOf(m) is not null)
            .GroupBy(static m => SlackRecords.DayOf(m)!, StringComparer.Ordinal) // why: the Where above keeps only messages whose day is not null.
            .Select(day => SlackRecords.Day(channel, workspace, day.Key, [.. day.OrderBy(static m => m.Ts, StringComparer.Ordinal)]))
            .OfType<ConnectorItem>();

    private sealed record ChannelRead(IReadOnlyList<ConnectorItem> Items, string? Newest, DateTimeOffset? RateLimitedUntil);
}
