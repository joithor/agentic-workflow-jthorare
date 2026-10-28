namespace Aw.Connectors.Core.Slack;

/// <summary>What one read of a channel's history or of a thread returned.</summary>
/// <param name="Items">The messages: history newest first, a thread oldest first (the root first).</param>
/// <param name="RateLimitedUntil">Set when Slack's rate limit stopped the read: <paramref name="Items"/> is incomplete and must not be committed. Null when the read reached its end.</param>
public sealed record SlackMessagePage(IReadOnlyList<SlackMessage> Items, DateTimeOffset? RateLimitedUntil);
