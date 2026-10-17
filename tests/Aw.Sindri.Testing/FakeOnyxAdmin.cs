using System.Net;
using System.Text;
using System.Text.Json;

namespace Aw.Sindri.Testing;

/// <summary>
/// An HTTP handler that behaves like the parts of Onyx v4.9.0 the provisioning client talks to (login, logout, /api/me, the user list, API key creation),
/// scriptable, and records every request it saw. It never listens on a socket. See docs/superpowers/specs/2026-10-16-sindri-onyx-managed-design.md §5.2.
/// </summary>
public sealed class FakeOnyxAdmin : HttpMessageHandler
{
    /// <summary>The name of Onyx's session cookie.</summary>
    public const string CookieName = "fastapiusersauth";

    private static readonly string[] _admin = ["admin"];
    private static readonly string[] _limited = ["write:chat"];

    private readonly List<Seen> _requests = [];

    /// <summary>One request the fake received.</summary>
    /// <param name="Method">The method.</param>
    /// <param name="PathAndQuery">The path and query.</param>
    /// <param name="Headers">Each header, name to joined values.</param>
    /// <param name="Body">The body text, empty for none.</param>
    /// <param name="ContentType">The body's content type, empty for none.</param>
    public sealed record Seen(HttpMethod Method, string PathAndQuery, IReadOnlyDictionary<string, string> Headers, string Body, string ContentType);

    /// <summary>Gets or sets the owner's email.</summary>
    public string OwnerEmail { get; set; } = "owner@example.com";

    /// <summary>Gets or sets the owner's password.</summary>
    public string OwnerPassword { get; set; } = "correct horse battery staple";

    /// <summary>Gets or sets the value of the session cookie a good login returns.</summary>
    public string CookieValue { get; set; } = "session-cookie-value-9d41";

    /// <summary>Gets or sets a value indicating whether `/api/me` lists `admin` for the owner.</summary>
    public bool OwnerIsAdmin { get; set; } = true;

    /// <summary>Gets the groups the user list shows for the owner (id and name).</summary>
    public IList<(int Id, string Name)> OwnerGroups { get; } = [(8, "Basic"), (7, "Admin")];

    /// <summary>Gets or sets the raw key a creation returns.</summary>
    public string MintedKey { get; set; } = "on_minted-key-0123456789abcdef";

    /// <summary>Gets or sets the id a creation returns.</summary>
    public int MintedKeyId { get; set; } = 41;

    /// <summary>Gets or sets a value indicating whether the minted key stops being accepted (a reset Onyx).</summary>
    public bool KeyRevoked { get; set; }

    /// <summary>Gets or sets a status that answers every login instead of the scripted one (400 and 401 are a refusal, 5xx an outage); null for the scripted behavior.</summary>
    public HttpStatusCode? LoginStatus { get; set; }

    /// <summary>Gets or sets a status that answers every key creation instead of the scripted one; null for the scripted behavior.</summary>
    public HttpStatusCode? CreateStatus { get; set; }

    /// <summary>Gets or sets a body that answers a good key creation instead of the key's JSON (for a garbled answer); null for the scripted one.</summary>
    public string? CreateBody { get; set; }

    /// <summary>Gets a value indicating whether the issued key is in the group whose id the request named as an admin one (a key made with no group is limited, as in Onyx).</summary>
    public bool IssuedKeyIsAdmin { get; private set; }

    /// <summary>Gets the requests received, in order.</summary>
    public IReadOnlyList<Seen> Requests => _requests;

    /// <summary>Gets the key the creation request has issued so far, or null.</summary>
    public string? IssuedKey { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            .ToDictionary(static h => h.Key, static h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
        var type = request.Content?.Headers.ContentType?.MediaType ?? string.Empty;
        var seen = new Seen(request.Method, request.RequestUri!.PathAndQuery, headers, body, type);
        _requests.Add(seen);
        return Route(seen);
    }

    private HttpResponseMessage Route(Seen seen)
        => (seen.Method.Method, seen.PathAndQuery) switch
        {
            ("POST", "/api/auth/login") => Login(seen),
            ("POST", "/api/auth/logout") => OwnerOnly(seen, static () => new HttpResponseMessage(HttpStatusCode.NoContent)),
            ("GET", "/api/me") => Me(seen),
            ("GET", "/api/manage/users/accepted/all") => OwnerOnly(seen, Users, adminOnly: true),
            ("POST", "/api/admin/api-key") => OwnerOnly(seen, () => Create(seen), adminOnly: true),
            ("GET", "/api/health") => new HttpResponseMessage(HttpStatusCode.OK),
            ("GET", "/api/version") => Json("""{"backend_version":"v4.9.0"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };

    private HttpResponseMessage Login(Seen seen)
    {
        if (LoginStatus is { } forced)
        {
            return new HttpResponseMessage(forced);
        }

        var form = seen.Body.Split('&').Select(static p => p.Split('=', 2)).Where(static p => p.Length == 2).ToDictionary(static p => WebUtility.UrlDecode(p[0]), static p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
        if (!form.TryGetValue("username", out var user) || !form.TryGetValue("password", out var password) || user != OwnerEmail || password != OwnerPassword)
        {
            return Json("""{"detail":"LOGIN_BAD_CREDENTIALS"}""", HttpStatusCode.BadRequest);
        }

        var ok = new HttpResponseMessage(HttpStatusCode.NoContent);
        _ = ok.Headers.TryAddWithoutValidation("Set-Cookie", $"{CookieName}={CookieValue}; HttpOnly; Path=/; SameSite=lax; Max-Age=3600");
        return ok;
    }

    private HttpResponseMessage OwnerOnly(Seen seen, Func<HttpResponseMessage> answer, bool adminOnly = false)
    {
        if (!seen.Headers.TryGetValue("Cookie", out var cookie) || !cookie.Contains($"{CookieName}={CookieValue}", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return adminOnly && !OwnerIsAdmin ? new HttpResponseMessage(HttpStatusCode.Forbidden) : answer();
    }

    private HttpResponseMessage Me(Seen seen)
    {
        if (seen.Headers.TryGetValue("Authorization", out var authorization))
        {
            var key = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization["Bearer ".Length..] : authorization;
            return IssuedKey is not null && key == IssuedKey && !KeyRevoked
                ? Json(JsonSerializer.Serialize(new { email = "api_key__sindri-onyx@onyxapikey.ai", effective_permissions = IssuedKeyIsAdmin ? _admin : _limited, account_type = "SERVICE_ACCOUNT" }))
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return OwnerOnly(seen, () => Json(JsonSerializer.Serialize(new { email = OwnerEmail, effective_permissions = OwnerIsAdmin ? _admin : _limited, account_type = "STANDARD" })));
    }

    private HttpResponseMessage Users()
        => Json(JsonSerializer.Serialize(new object[]
        {
            new { email = "someone-else@example.com", is_admin = false, groups = new[] { new { id = 8, name = "Basic" } } },
            new { email = OwnerEmail, is_admin = OwnerIsAdmin, groups = OwnerGroups.Select(static g => new { id = g.Id, name = g.Name }).ToArray() },
        }));

    private HttpResponseMessage Create(Seen seen)
    {
        if (CreateStatus is { } forced)
        {
            return new HttpResponseMessage(forced) { Content = new StringContent("internal error with " + MintedKey) };
        }

        IssuedKey = MintedKey;
        using var document = JsonDocument.Parse(seen.Body);
        var name = document.RootElement.GetProperty("name").GetString();
        var groups = document.RootElement.GetProperty("group_ids").EnumerateArray().Select(static g => g.GetInt32()).ToList();
        IssuedKeyIsAdmin = OwnerGroups.Any(g => g.Name == "Admin" && groups.Contains(g.Id));
        return CreateBody is { } garbled ? Json(garbled) : Json(JsonSerializer.Serialize(new { api_key_id = MintedKeyId, api_key = MintedKey, api_key_name = name, api_key_display = "on_****", user_id = "00000000-0000-0000-0000-000000000001" }));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
