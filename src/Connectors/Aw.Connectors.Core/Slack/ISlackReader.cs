using Aw.Kernel;

namespace Aw.Connectors.Core.Slack;

/// <summary>Reads the messages of the channels the profile lists. Every call refuses a channel that is not in the allowlist, before any request (SND-INDEX-006).</summary>
public interface ISlackReader
{
    /// <summary>Reads a channel's top-level messages (conversations.history), newest first, every page from <paramref name="oldest"/> (inclusive) to now.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="oldest">The ts to start from (inclusive), or null for the whole channel.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The messages (a rate limit is a value in the page, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, CancellationToken cancellationToken);

    /// <summary>Reads a thread (conversations.replies): the root, then its replies, oldest first.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="threadTs">The root's ts.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The messages (a rate limit is a value in the page, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken);

    /// <summary>Checks the token and the channel with one cheap call (conversations.history, one message).</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>Success (a rate limit is a value: its reset time), or the error that says what is wrong.</returns>
    public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken);
}
