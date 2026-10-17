using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// IOnyxProvisioning over HTTP to http://127.0.0.1:&lt;port&gt; only (built here from a port, never from input), through the seven-route allowlist
/// (<see cref="OnyxProvisioningRoutes"/>): a request off the list throws before anything is sent. 10 s per request, no redirect, no proxy. No message or exception
/// carries the password, the cookie, the key or an answer body: errors say what failed and what to run.
/// </summary>
/// <param name="client">The client (production: <see cref="CreateClient"/>).</param>
internal sealed class HttpOnyxProvisioning(HttpClient client) : IOnyxProvisioning
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(OnyxProvisioningJsonContext.Default);
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private const string AdminAccess = "admin";
    private const string AdminGroup = "Admin";
    private const string ServiceAccount = "SERVICE_ACCOUNT";

    /// <summary>Builds the client production uses: no redirect is followed and no proxy is used.</summary>
    /// <returns>The client.</returns>
    internal static HttpClient CreateClient() => HttpOnyxHealth.CreateClient();

    /// <summary>Sends one allowlisted request and reads the whole answer.</summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="method">The method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="prepare">Adds the credential and the body; null for a bare request.</param>
    /// <returns>The status, the body and the cookies the answer set.</returns>
    /// <exception cref="InvalidOperationException">The request isn't on the allowlist (nothing was sent).</exception>
    internal async Task<ProvisioningReply> Send(int port, HttpMethod method, string pathAndQuery, CancellationToken cancellationToken, Action<HttpRequestMessage>? prepare = null)
    {
        if (!OnyxProvisioningRoutes.Allowed(method, pathAndQuery))
        {
            throw new InvalidOperationException(
                $"The onyx provisioning client refused to send {method} {pathAndQuery}: it isn't on its allowlist "
                + $"({string.Join(", ", OnyxProvisioningRoutes.Allowlist.Select(static r => $"{r.Method} {r.Path}"))}; spec §5.2).");
        }

        using var request = new HttpRequestMessage(method, new Uri(Origin(port), pathAndQuery));
        prepare?.Invoke(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        using var response = await client.SendAsync(request, deadline.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
        return new ProvisioningReply(response.StatusCode, body, cookies);
    }

    /// <inheritdoc />
    public async ValueTask<Result<OwnerSession>> Login(int port, string email, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);
        var reply = await Call(port, HttpMethod.Post, "/api/auth/login", request =>
            request.Content = new FormUrlEncodedContent([KeyValuePair.Create("username", email), KeyValuePair.Create("password", password)]), cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        var answered = reply.Value;
        if (answered.Status is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            return SindriErrors.OnyxKeyFailed.With("Onyx didn't accept that email and password (an account that failed to log in too often can be locked out for a while); " + OnyxFixes.KeyFailed);
        }

        if (!IsSuccess(answered.Status) || SessionCookie(answered) is not { } cookie)
        {
            return Failed($"Onyx's login answered {Describe(answered.Status)} without a session");
        }

        return new OwnerSession(cookie);
    }

    /// <inheritdoc />
    public async ValueTask<Result<OnyxAccount>> Me(int port, OnyxCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        var reply = await Call(port, HttpMethod.Get, "/api/me", request => Authenticate(request, credential), cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!IsSuccess(reply.Value.Status))
        {
            return Failed($"Onyx didn't accept the credential for /api/me ({Describe(reply.Value.Status)})");
        }

        var me = AwJson.Deserialize<MeWire>(reply.Value.Body, _json);
        return me.IsError
            ? Failed("Onyx's /api/me answer isn't what it should be")
            : new OnyxAccount(me.Value.Email ?? string.Empty, me.Value.EffectivePermissions?.Contains(AdminAccess, StringComparer.Ordinal) == true, string.Equals(me.Value.AccountType, ServiceAccount, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async ValueTask<Result<OnyxMintedKey>> MintAdminKey(int port, OwnerSession session, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(name);
        var owner = await Me(port, session, cancellationToken).ConfigureAwait(false);
        if (owner.IsError)
        {
            return owner.Error;
        }

        if (!owner.Value.IsAdmin)
        {
            return NotAdmin();
        }

        var group = await AdminGroupId(port, session, owner.Value.Email, cancellationToken).ConfigureAwait(false);
        return group.IsError ? group.Error : await Create(port, session, name, group.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Logout(int port, OwnerSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var reply = await Call(port, HttpMethod.Post, "/api/auth/logout", request => Authenticate(request, session), cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        return IsSuccess(reply.Value.Status) ? Results.Ok() : Failed($"Onyx's logout answered {Describe(reply.Value.Status)}");
    }

    // The owner's row in the user list names the groups the owner is in, default groups included; its `Admin` group's id is what the key must join.
    private async ValueTask<Result<int>> AdminGroupId(int port, OwnerSession session, string email, CancellationToken cancellationToken)
    {
        var reply = await Call(port, HttpMethod.Get, "/api/manage/users/accepted/all", request => Authenticate(request, session), cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (reply.Value.Status is HttpStatusCode.Forbidden)
        {
            return NotAdmin();
        }

        var users = IsSuccess(reply.Value.Status) ? AwJson.Deserialize<List<UserWire>>(reply.Value.Body, _json) : null;
        if (users is not { IsSuccess: true })
        {
            return Failed($"Onyx's user list answered {Describe(reply.Value.Status)} or something unexpected");
        }

        var admin = users.Value.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))?.Groups?.FirstOrDefault(static g => string.Equals(g.Name, AdminGroup, StringComparison.Ordinal));
        return admin is null ? NotAdmin() : admin.Id;
    }

    private async ValueTask<Result<OnyxMintedKey>> Create(int port, OwnerSession session, string name, int group, CancellationToken cancellationToken)
    {
        var body = AwJson.Serialize(new KeyRequestWire(name, [group]), _json);
        var reply = await Call(port, HttpMethod.Post, "/api/admin/api-key", request =>
        {
            Authenticate(request, session);
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }, cancellationToken).ConfigureAwait(false);
        if (reply.IsError)
        {
            return reply.Error;
        }

        if (!IsSuccess(reply.Value.Status))
        {
            return Failed($"Onyx refused to create the key ({Describe(reply.Value.Status)})");
        }

        var key = AwJson.Deserialize<KeyWire>(reply.Value.Body, _json);
        return key is { IsSuccess: true, Value.ApiKey.Length: > 0 }
            ? new OnyxMintedKey(key.Value.ApiKey, key.Value.ApiKeyId)
            : Failed("Onyx's key creation answer isn't what it should be");
    }

    // One request through the allowlisted path; a server that isn't answering or is too slow is 008, the caller's own cancellation is thrown.
    private async ValueTask<Result<ProvisioningReply>> Call(int port, HttpMethod method, string path, Action<HttpRequestMessage> prepare, CancellationToken cancellationToken)
    {
        try
        {
            return await Send(port, method, path, cancellationToken, prepare).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(string.Create(CultureInfo.InvariantCulture, $"Onyx didn't answer {path} within {_timeout.TotalSeconds:0} s"));
        }
        catch (HttpRequestException)
        {
            return Failed($"Onyx isn't answering {path}");
        }
    }

    private static void Authenticate(HttpRequestMessage request, OnyxCredential credential)
    {
        switch (credential)
        {
            case OwnerSession session:
                request.Headers.Add("Cookie", $"{SessionCookieName}={session.Cookie}");
                break;
            case ApiKeyCredential key:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key.Key);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(credential), credential, "unknown credential");
        }
    }

    // The value of Onyx's session cookie (fastapi-users' default name), from the Set-Cookie headers of the login answer.
    private static string? SessionCookie(ProvisioningReply reply)
    {
        var prefix = SessionCookieName + "=";
        var line = reply.SetCookies.FirstOrDefault(static c => c.StartsWith(SessionCookieName + "=", StringComparison.Ordinal));
        if (line is null)
        {
            return null;
        }

        var value = line[prefix.Length..];
        var end = value.IndexOf(';', StringComparison.Ordinal);
        value = end < 0 ? value : value[..end];
        return value.Length > 0 ? value : null;
    }

    private const string SessionCookieName = "fastapiusersauth";

    private static Uri Origin(int port) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"));

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;

    private static string Describe(HttpStatusCode status) => string.Create(CultureInfo.InvariantCulture, $"{(int)status}");

    private static Error Failed(string what) => SindriErrors.OnyxKeyFailed.With($"{what}; {OnyxFixes.KeyFailed}");

    private static Error NotAdmin() => SindriErrors.OnyxLoginNotAdmin.With("that Onyx account isn't an admin; " + OnyxFixes.LoginNotAdmin);
}
