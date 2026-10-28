using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features.PullSlack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features;

/// <summary>The Slack source as an <see cref="IConnector"/> (stub, red first).</summary>
internal sealed class SlackConnector : IConnector
{
    public SlackConnector(SlackSettings settings, ISlackReader reader, IService<PullSlackRequest, PullSlackResponse> pull)
    {
        _ = (settings, reader, pull);
    }

    public ConnectorDescription Describe() => throw new NotSupportedException();

    public ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
}
