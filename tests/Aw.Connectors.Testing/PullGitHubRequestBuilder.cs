using Aw.Connectors.Features.PullGitHub;

namespace Aw.Connectors.Testing;

/// <summary>Builds a PullGitHubRequest with deterministic defaults.</summary>
public sealed class PullGitHubRequestBuilder
{
    private readonly PullGitHubRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public PullGitHubRequest Build() => _request;
}
