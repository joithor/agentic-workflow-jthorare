using Aw.Connectors.Core.Slack;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>Reads channels from the Slack Web API (stub, red first).</summary>
internal sealed class SlackClient : ISlackReader
{
    /// <summary>Creates the client.</summary>
    /// <param name="http">The client; its handler must not follow redirects.</param>
    /// <param name="settings">The allowlist and the token.</param>
    /// <param name="clock">The clock a rate-limit wait is counted from.</param>
    public SlackClient(HttpClient http, SlackClientSettings settings, TimeProvider clock)
    {
        _ = (http, settings, clock, SlackHostPolicy.IsAllowed(new Uri("https://slack.com/api")));
    }

    /// <inheritdoc />
    public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken) => throw new NotSupportedException();
}
