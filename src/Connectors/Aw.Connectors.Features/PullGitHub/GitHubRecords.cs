using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>Maps a GitHub issue or pull request onto a <see cref="ConnectorItem"/>: its record key, cleaned text, permalink and 'closes' links (owned index spec §5.3).</summary>
internal static partial class GitHubRecords
{
    public static ConnectorItem From(string repo, GitHubIssue issue)
    {
        var title = OneLine(TextCleaner.Clean(issue.Title));
        var body = TextCleaner.Clean(issue.Body);
        return new ConnectorItem(
            Key(repo, issue.IsPullRequest, issue.Number),
            issue.IsPullRequest ? "pr" : "issue",
            title,
            body.Length == 0 ? title : $"{title}\n\n{body}",
            issue.Author,
            Permalink(issue.Url),
            string.Equals(issue.State, "closed", StringComparison.Ordinal) ? "closed" : "open",
            issue.State,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.ClosedAt,
            issue.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            issue.IsPullRequest ? Closes(repo, issue.Number, issue.Body) : [],
            repo);
    }

    private static string Key(string repo, bool pullRequest, int number)
        => string.Create(CultureInfo.InvariantCulture, $"github:{(pullRequest ? "pr" : "issue")}/{repo}/{number}");

    private static string? Permalink(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static List<ConnectorLink> Closes(string repo, int own, string body)
    {
        var numbers = new SortedSet<int>();
        foreach (Match match in ClosingReference.Matches(body))
        {
            if (int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n != own)
            {
                _ = numbers.Add(n);
            }
        }

        return [.. numbers.Select(n => new ConnectorLink("closes", Key(repo, pullRequest: false, n)))];
    }

    [GeneratedRegex(@"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s+#(?<n>\d{1,9})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ClosingReference { get; }
}
