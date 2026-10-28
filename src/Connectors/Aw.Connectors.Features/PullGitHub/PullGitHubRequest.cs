using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>Reads one repo's issues and pull requests changed since a cursor.</summary>
/// <param name="Repo">'owner/name'.</param>
/// <param name="After">Where to start; <see cref="GitHubCursor.From"/> for a first run.</param>
/// <param name="Limit">Stop after the page that brings the run to this many entries.</param>
public sealed record PullGitHubRequest(string Repo, GitHubCursor After, int Limit);
