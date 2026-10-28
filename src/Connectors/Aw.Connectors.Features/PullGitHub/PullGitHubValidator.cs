using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullGitHub;

internal sealed partial class PullGitHubValidator : IValidator<PullGitHubRequest>
{
    private const int MaxLimit = 10_000;

    public Result<Unit> Validate(PullGitHubRequest value)
    {
        if (!RepoName.IsMatch(value.Repo))
        {
            return ConnectorsErrors.Invalid.With("repo must be 'owner/name'.");
        }

        return value.Limit is < 1 or > MaxLimit
            ? ConnectorsErrors.Invalid.With($"limit must be from 1 to {MaxLimit}.")
            : Unit.Value;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoName { get; }
}
