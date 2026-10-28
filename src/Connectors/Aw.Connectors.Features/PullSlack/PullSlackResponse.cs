using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>What a Slack pull read.</summary>
/// <param name="Items">The records of the channels that were read whole.</param>
/// <param name="Next">Where the next pull starts: the channels read whole have moved, the channel the rate limit stopped has not.</param>
/// <param name="RateLimitedUntil">Set when Slack's rate limit stopped the run; the next pull should wait until then.</param>
internal sealed record PullSlackResponse(IReadOnlyList<ConnectorItem> Items, SlackCursor Next, DateTimeOffset? RateLimitedUntil);
