using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>Reads GitHub's replies: headers, the Link header, and the entries of the issue list.</summary>
internal static partial class GitHubWire
{
    public static string? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    public static Uri? NextLink(HttpResponseMessage response)
    {
        var link = Header(response, "Link");
        return link is not null && NextRel.Match(link) is { Success: true } m && Uri.TryCreate(m.Groups["url"].Value, UriKind.Absolute, out var next) ? next : null;
    }

    public static bool SameOrigin(Uri a, Uri b)
        => string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;

    /// <summary>Reads one entry; null when it lacks the id, number or a date the cursor needs.</summary>
    public static GitHubIssue? ReadIssue(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var (id, number, created, updated) = (Long(e, "id"), Long(e, "number"), Date(e, "created_at"), Date(e, "updated_at"));
        if (id is null || number is null || created is null || updated is null)
        {
            return null;
        }

        return new GitHubIssue(
            id.Value, (int)number.Value, e.TryGetProperty("pull_request", out _), Str(e, "title"), Str(e, "body"), Str(e, "state"), Author(e), Str(e, "html_url"),
            created.Value, updated.Value, Date(e, "closed_at"), Labels(e), (int)Math.Clamp(Long(e, "comments").GetValueOrDefault(), 0, int.MaxValue));
    }

    private static long? Long(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v) ? v : null;

    private static string? Text(JsonElement e, string name)
        => e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string Str(JsonElement e, string name) => Text(e, name) ?? string.Empty;

    private static DateTimeOffset? Date(JsonElement e, string name)
        => DateTimeOffset.TryParse(Text(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static string? Author(JsonElement e)
        => e.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object ? Text(user, "login") : null;

    private static List<string> Labels(JsonElement e)
    {
        List<string> names = [];
        if (e.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
        {
            names.AddRange(labels.EnumerateArray().Select(static l => l.ValueKind == JsonValueKind.String ? l.GetString() : Text(l, "name")).OfType<string>());
        }

        return names;
    }

    [GeneratedRegex("<(?<url>[^>]+)>\\s*;\\s*rel=\"next\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex NextRel { get; }
}
