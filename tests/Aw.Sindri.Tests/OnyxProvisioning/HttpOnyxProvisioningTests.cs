using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxProvisioning;

// What only this client can get wrong: the exact requests it sends and what it never lets out.
[Lane(TestLane.Unit)]
public sealed class HttpOnyxProvisioningTests : IDisposable
{
    private readonly FakeOnyxAdmin _admin = new();
    private readonly HttpClient _http;
    private readonly HttpOnyxProvisioning _client;

    public HttpOnyxProvisioningTests()
    {
        _http = new HttpClient(_admin);
        _client = new HttpOnyxProvisioning(_http);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _http.Dispose();
        _admin.Dispose();
    }

    private async Task<OwnerSession> LoggedIn() => (await _client.Login(3000, _admin.OwnerEmail, _admin.OwnerPassword, Token)).Value;

    [Fact]
    public async Task Login_posts_the_form_to_api_auth_login_and_keeps_the_fastapiusersauth_cookie()
    {
        using var session = await LoggedIn();
        _ = await _client.Me(3000, session, Token);

        var login = _admin.Requests[0];
        Assert.Equal((HttpMethod.Post, "/api/auth/login", "application/x-www-form-urlencoded"), (login.Method, login.PathAndQuery, login.ContentType));
        var form = login.Body.Split('&').Select(static p => p.Split('=', 2)).ToDictionary(static p => WebUtility.UrlDecode(p[0]), static p => WebUtility.UrlDecode(p[1]), StringComparer.Ordinal);
        Assert.Equal(_admin.OwnerEmail, form["username"]);
        Assert.Equal(_admin.OwnerPassword, form["password"]);
        Assert.Equal("fastapiusersauth=" + _admin.CookieValue, _admin.Requests[1].Headers["Cookie"]);
        Assert.Equal("http://127.0.0.1:3000/api/auth/login", "http://127.0.0.1:3000" + login.PathAndQuery);
    }

    [Fact]
    public async Task MintAdminKey_creates_the_key_with_the_admin_group_id_found_through_the_owners_snapshot()
    {
        using var session = await LoggedIn();
        var before = _admin.Requests.Count;

        var minted = await _client.MintAdminKey(3000, session, "sindri-onyx", Token);

        Assert.Equal(_admin.MintedKey, minted.Value.Key);
        Assert.Equal(_admin.MintedKeyId, minted.Value.ApiKeyId);
        var sent = _admin.Requests.Skip(before).Select(static r => (r.Method.Method, r.PathAndQuery)).ToArray();
        Assert.Equal([("GET", "/api/me"), ("GET", "/api/manage/users/accepted/all"), ("POST", "/api/admin/api-key")], sent);
        var create = _admin.Requests[^1];
        Assert.Equal("""{"name":"sindri-onyx","group_ids":[7]}""", create.Body);
        Assert.Equal("application/json", create.ContentType);
        Assert.True(_admin.IssuedKeyIsAdmin);
    }

    [Fact]
    public async Task A_login_that_is_not_an_admin_is_SND_ONYX_007()
    {
        _admin.OwnerIsAdmin = false;
        using var plain = await LoggedIn();
        var notAdmin = await _client.MintAdminKey(3000, plain, "sindri-onyx", Token);
        _admin.OwnerIsAdmin = true;
        _ = _admin.OwnerGroups.Remove((7, "Admin"));
        using var noGroup = await LoggedIn();

        var noRow = await _client.MintAdminKey(3000, noGroup, "sindri-onyx", Token);

        Assert.Equal("SND-ONYX-007", notAdmin.Error.Code);
        Assert.Equal("SND-ONYX-007", noRow.Error.Code);
        Assert.DoesNotContain(_admin.Requests, static r => r.Method == HttpMethod.Post && r.PathAndQuery == "/api/admin/api-key");
    }

    [Fact]
    public async Task Me_with_a_key_sends_Bearer_and_reports_admin_and_service_account()
    {
        using var session = await LoggedIn();
        var minted = (await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Value;

        var account = (await _client.Me(3000, new ApiKeyCredential(minted.Key), Token)).Value;

        Assert.True(account.IsAdmin);
        Assert.True(account.IsServiceAccount);
        Assert.Equal("Bearer " + minted.Key, _admin.Requests[^1].Headers["Authorization"]);
        Assert.False(_admin.Requests[^1].Headers.ContainsKey("Cookie"));
    }

    [Fact]
    public async Task The_admin_group_id_is_read_from_the_snapshot_not_assumed()
    {
        using var session = await LoggedIn();
        _ = _admin.OwnerGroups.Remove((7, "Admin"));
        _admin.OwnerGroups.Add((9, "Admin"));   // the snapshot names the Admin group 9, so 9 is what the request must carry
        var minted = (await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Value;

        Assert.True((await _client.Me(3000, new ApiKeyCredential(minted.Key), Token)).Value.IsAdmin);
        Assert.Contains("[9]", _admin.Requests.First(static r => r.Method == HttpMethod.Post && r.PathAndQuery == "/api/admin/api-key").Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_request_sent_is_on_the_allowlist()
    {
        using var session = await LoggedIn();
        var minted = (await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Value;
        _ = await _client.Me(3000, new ApiKeyCredential(minted.Key), Token);
        _ = await _client.Me(3000, session, Token);
        _ = await _client.Logout(3000, session, Token);

        Assert.NotEmpty(_admin.Requests);
        Assert.All(_admin.Requests, static r => Assert.True(OnyxProvisioningRoutes.Allowed(r.Method, r.PathAndQuery), $"{r.Method} {r.PathAndQuery}"));
        Assert.All(_admin.Requests, static r => Assert.NotEqual("/api/auth/register", r.PathAndQuery, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("DELETE", "/api/admin/api-key/1")]
    [InlineData("PATCH", "/api/admin/api-key/1")]
    [InlineData("GET", "/api/manage/admin/user-group")]
    [InlineData("GET", "/api/me?x=1")]
    public async Task An_off_list_request_is_refused_before_it_is_sent(string method, string path)
    {
        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await _client.Send(3000, new HttpMethod(method), path, Token));

        Assert.Empty(_admin.Requests);
    }

    [Fact]
    public async Task The_password_cookie_and_key_never_appear_in_ToString_or_error_messages()
    {
        const string wrong = "a-wrong-password-9ff2";
        using var session = await LoggedIn();
        var minted = (await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Value;
        var errors = new List<string>
        {
            (await _client.Login(3000, _admin.OwnerEmail, wrong, Token)).Error.Message,
        };
        _admin.LoginStatus = HttpStatusCode.InternalServerError;
        errors.Add((await _client.Login(3000, _admin.OwnerEmail, _admin.OwnerPassword, Token)).Error.Message);
        _admin.LoginStatus = null;
        _admin.CreateStatus = HttpStatusCode.InternalServerError;
        errors.Add((await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Error.Message);
        _admin.CreateStatus = null;
        _admin.CreateBody = "not json " + _admin.MintedKey;
        errors.Add((await _client.MintAdminKey(3000, session, "sindri-onyx", Token)).Error.Message);
        errors.Add((await _client.Me(3000, new ApiKeyCredential("on_revoked-key"), Token)).Error.Message);
        errors.AddRange([session.ToString(), minted.ToString(), new ApiKeyCredential(minted.Key).ToString()]);

        foreach (var secret in new[] { _admin.OwnerPassword, wrong, _admin.CookieValue, _admin.MintedKey, "on_revoked-key" })
        {
            Assert.All(errors, text => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_login_Onyx_does_not_accept_is_008_with_the_run_again_fix(HttpStatusCode status)
    {
        _admin.LoginStatus = status;

        var error = (await _client.Login(3000, _admin.OwnerEmail, _admin.OwnerPassword, Token)).Error;

        Assert.Equal("SND-ONYX-008", error.Code);
        Assert.Contains("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_key_creation_that_fails_or_answers_garbage_is_008_without_a_body()
    {
        using var session = await LoggedIn();
        _admin.CreateStatus = HttpStatusCode.BadGateway;
        var failed = await _client.MintAdminKey(3000, session, "sindri-onyx", Token);
        _admin.CreateStatus = null;
        _admin.CreateBody = "<html>nope</html>";

        var garbled = await _client.MintAdminKey(3000, session, "sindri-onyx", Token);

        Assert.Equal("SND-ONYX-008", failed.Error.Code);
        Assert.Equal("SND-ONYX-008", garbled.Error.Code);
    }

    [Fact]
    public async Task A_disposed_session_cannot_be_used_again()
    {
        var session = await LoggedIn();
        session.Dispose();

        _ = await Assert.ThrowsAsync<ObjectDisposedException>(async () => await _client.Me(3000, session, Token));
    }
}
