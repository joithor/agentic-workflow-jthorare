using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>
/// Reads a repo's issue list (pull requests included) from the GitHub REST API (owned index spec §5.3): GET /repos/{o}/{r}/issues oldest change first, from the
/// cursor's instant (inclusive), a page at a time by the Link header. A rate limit stops the run and comes back as a value with the reset time (never a sleep). The host rules
/// are checked before the first request; a redirect is refused; the token goes only in the Authorization header and in no error.
/// </summary>
public sealed partial class GitHubClient : IGitHubReader
{
    private const string DefaultRoot = "https://api.github.com";
    private const int DefaultWaitSeconds = 60;
    private readonly HttpClient _http;
    private readonly GitHubClientSettings _settings;
    private readonly IHostResolver _resolver;
    private readonly TimeProvider _clock;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The client; its handler must not follow redirects (see <see cref="GitHubHttp.CreateHandler"/>).</param>
    /// <param name="settings">The host, the private-host flag and the token.</param>
    /// <param name="resolver">Resolves the host name for the host rules.</param>
    /// <param name="clock">The clock a rate-limit wait is counted from.</param>
    public GitHubClient(HttpClient http, GitHubClientSettings settings, IHostResolver resolver, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(clock);
        _http = http;
        _settings = settings;
        _resolver = resolver;
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);
        if (repo is null || !RepoName.IsMatch(repo))
        {
            return ConnectorsErrors.Invalid.With("The repo must be 'owner/name'.");
        }

        if (_settings.Token is { Length: > 0 } token && token.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return ConnectorsErrors.Invalid.With("The GitHub token has whitespace or control characters; point the profile at the token itself.");
        }

        var root = Root();
        if (root.IsError)
        {
            return root.Error;
        }

        if (await CheckHost(root.Value, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        return await Read(root.Value, repo, after, limit, cancellationToken).ConfigureAwait(false);
    }

    private Result<Uri> Root()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            return new Uri(DefaultRoot);
        }

        var host = _settings.Host.Trim();
        if (host.Any(static c => char.IsWhiteSpace(c) || c is '/' or '\\' or '@' or '?' or '#')
            || !Uri.TryCreate("https://" + host, UriKind.Absolute, out var parsed) || parsed.AbsolutePath != "/")
        {
            return ConnectorsErrors.Invalid.With("context.sources.github.host must be a host name with an optional port (no scheme, path or user).");
        }

        return new Uri($"https://{parsed.Authority}/api/v3");
    }

    private async ValueTask<Error?> CheckHost(Uri root, CancellationToken cancellationToken)
    {
        if (_settings.AllowPrivateHost)
        {
            return null;
        }

        if (root.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            return ConnectorsErrors.HostRefused.With($"{root.Host} is an IP literal; set context.sources.github.allowPrivateHost if that is intended.");
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await _resolver.Resolve(root.DnsSafeHost, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return ConnectorsErrors.Unreachable.With($"{root.DnsSafeHost} did not resolve.");
        }

        if (addresses.Count == 0)
        {
            return ConnectorsErrors.Unreachable.With($"{root.DnsSafeHost} did not resolve.");
        }

        return addresses.All(HostPolicy.IsPublic)
            ? null
            : ConnectorsErrors.HostRefused.With($"{root.DnsSafeHost} resolves to a private, loopback or link-local address; set context.sources.github.allowPrivateHost if that is intended.");
    }

    private async ValueTask<Result<GitHubPullPage>> Read(Uri root, string repo, GitHubCursor after, int limit, CancellationToken cancellationToken)
    {
        var since = Uri.EscapeDataString(after.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        Uri? url = new($"{root.AbsoluteUri.TrimEnd('/')}/repos/{repo}/issues?state=all&sort=updated&direction=asc&since={since}&per_page=100");
        List<GitHubIssue> items = [];
        while (url is not null)
        {
            var step = await Fetch(url, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } until)
            {
                return new GitHubPullPage(items, after.Advance(items), RateLimitedUntil: until);
            }

            items.AddRange(step.Value.Items.Where(after.Admits));
            url = items.Count >= limit ? null : step.Value.Next;
        }

        return new GitHubPullPage(items, after.Advance(items), RateLimitedUntil: null);
    }

    private async ValueTask<Result<Step>> Fetch(Uri url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("agentic-workflow-sindri");
        _ = request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        if (_settings.Token is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await Interpret(response, url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            return ConnectorsErrors.Unreachable.With($"GitHub at {url.Host} could not be reached ({e.HttpRequestError}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConnectorsErrors.Unreachable.With($"GitHub at {url.Host} did not answer in time.");
        }
    }

    private async ValueTask<Result<Step>> Interpret(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status is >= 300 and < 400)
        {
            return ConnectorsErrors.RedirectRefused.With(string.Create(CultureInfo.InvariantCulture, $"GitHub at {url.Host} answered {status}; redirects are never followed."));
        }

        if (status == 200)
        {
            return await Parse(response, url, cancellationToken).ConfigureAwait(false);
        }

        if (IsRateLimit(response))
        {
            return new Step(Items: [], Next: null, RateLimitedUntil: RateLimitReset(response));
        }

        return status switch
        {
            401 => ConnectorsErrors.Unauthorized.With("GitHub rejected the token; check context.sources.github.token (a read-only token with Issues, Pull requests and Metadata read)."),
            403 => ConnectorsErrors.AccessDenied.With("GitHub refused the request; the token may lack read access to the repo."),
            404 => ConnectorsErrors.NotFound.With("GitHub has no such repo, or the token cannot see it."),
            _ => ConnectorsErrors.BadReply.With(string.Create(CultureInfo.InvariantCulture, $"GitHub answered {status}.")),
        };
    }

    private static async ValueTask<Result<Step>> Parse(HttpResponseMessage response, Uri url, CancellationToken cancellationToken)
    {
        var next = GitHubWire.NextLink(response);
        if (next is not null && !GitHubWire.SameOrigin(next, url))
        {
            return ConnectorsErrors.BadReply.With("GitHub's next page is on another host; it is not followed.");
        }

        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ConnectorsErrors.BadReply.With("GitHub's issue list was not a JSON array.");
            }

            var issues = document.RootElement.EnumerateArray().Select(GitHubWire.ReadIssue).ToList();
            return issues.Contains(null)
                ? ConnectorsErrors.BadReply.With("An entry of GitHub's issue list lacked a field the connector reads.")
                : new Step([.. issues.OfType<GitHubIssue>()], next, RateLimitedUntil: null);
        }
        catch (JsonException)
        {
            return ConnectorsErrors.BadReply.With("GitHub's issue list was not valid JSON.");
        }
    }

    private static bool IsRateLimit(HttpResponseMessage response)
        => response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.Forbidden && (GitHubWire.Header(response, "x-ratelimit-remaining") == "0" || GitHubWire.Header(response, "retry-after") is not null));

    private DateTimeOffset RateLimitReset(HttpResponseMessage response)
    {
        var now = _clock.GetUtcNow();
        if (int.TryParse(GitHubWire.Header(response, "retry-after"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return now.AddSeconds(seconds);
        }

        return long.TryParse(GitHubWire.Header(response, "x-ratelimit-reset"), NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : now.AddSeconds(DefaultWaitSeconds);
    }

    [GeneratedRegex("^(?!\\.{1,2}/)(?!.*/\\.{1,2}$)[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RepoName { get; }

    private sealed record Step(IReadOnlyList<GitHubIssue> Items, Uri? Next, DateTimeOffset? RateLimitedUntil);
}
