namespace Aw.Connectors.Features.PullSlack;

/// <summary>Maps Slack threads and day buckets onto connector records (stub, red first).</summary>
internal static class SlackRecords
{
    /// <summary>The audience key of a channel's records: 'slack:&lt;channelId&gt;'.</summary>
    /// <param name="channelId">The channel id.</param>
    /// <returns>The key.</returns>
    public static string AudienceKey(string channelId) => "slack:" + channelId;
}
