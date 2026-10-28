using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Kernel;

namespace Aw.Context.Core.Audience;

/// <summary>
/// <see cref="IAudiencePolicy"/> over the approved profile (spec §4.4). A session scoped to a profile repo reads: that repo's own audience
/// ('repo:&lt;owner&gt;/&lt;repo&gt;') and the profile's Linear teams ('linear:&lt;team key&gt;'). Slack channels are left out on purpose: the spec lets a Slack thread
/// reach a session only through a link to the repo's graph nodes (rule c), which the audience mask can't see. A repo the profile doesn't name reads nothing.
/// </summary>
/// <param name="settings">The approved profile's 'context:' section.</param>
/// <param name="profileRepos">The repos the profile names.</param>
public sealed class ProfileAudiencePolicy(ContextSettings settings, IReadOnlyCollection<ContextRepoName> profileRepos) : IAudiencePolicy
{
    /// <summary>
    /// The audience of a profile repo's own records: the one 'context.sources.github.repos' entry ('owner/name') whose name part equals the profile
    /// repo's name, ignoring case. The repo-docs feed and the mask both call this, so they can't disagree. No entry, or entries of two owners, is no
    /// audience (null): a record stored under it is visible to no session.
    /// </summary>
    /// <param name="repo">The profile repo.</param>
    /// <param name="githubRepos">The 'owner/name' entries of 'context.sources.github.repos'.</param>
    /// <returns>The audience key, or null.</returns>
    public static AudienceKey? OwnRepo(ContextRepoName repo, IReadOnlyList<string> githubRepos)
    {
        ArgumentNullException.ThrowIfNull(githubRepos);
        var matches = githubRepos
            .Select(entry => entry.Split('/'))
            .Where(parts => parts.Length == 2 && string.Equals(parts[1], repo.Value, StringComparison.OrdinalIgnoreCase))
            .Select(parts => "repo:" + parts[0] + "/" + parts[1])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return matches.Count == 1 && AudienceKey.TryFrom(matches[0], out var key) ? key : null;
    }

    /// <inheritdoc />
    public ValueTask<Result<AudienceScope>> For(ContextRepoName repo, CancellationToken cancellationToken)
    {
        if (!profileRepos.Contains(repo))
        {
            return ValueTask.FromResult<Result<AudienceScope>>(AudienceScope.None);
        }

        var allowed = new HashSet<AudienceKey>();
        if (OwnRepo(repo, settings.GitHub?.Repos ?? []) is { } own)
        {
            _ = allowed.Add(own);
        }

        foreach (var team in settings.LinearTeams)
        {
            if (AudienceKey.TryFrom("linear:" + team, out var key))
            {
                _ = allowed.Add(key);
            }
        }

        return ValueTask.FromResult<Result<AudienceScope>>(new AudienceScope(allowed));
    }
}
