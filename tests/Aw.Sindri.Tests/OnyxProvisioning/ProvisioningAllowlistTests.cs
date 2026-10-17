using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Onyx;

namespace Aw.Sindri.Tests.OnyxProvisioning;

// The provisioning client holds the owner's login and an admin key; its list of requests is its own, never the query adapter's.
[Lane(TestLane.Unit)]
public sealed class ProvisioningAllowlistTests
{
    [Fact]
    public void The_allowlist_has_exactly_seven_routes_and_no_register()
    {
        (string Method, string Path)[] expected =
        [
            ("GET", "/api/health"),
            ("GET", "/api/version"),
            ("POST", "/api/auth/login"),
            ("POST", "/api/auth/logout"),
            ("GET", "/api/me"),
            ("GET", "/api/manage/users/accepted/all"),
            ("POST", "/api/admin/api-key"),
        ];

        var listed = OnyxProvisioningRoutes.Allowlist.Select(static r => (r.Method.Method, r.Path)).Order().ToArray();

        Assert.Equal(expected.Order().ToArray(), listed);
        Assert.DoesNotContain(listed, static r => r.Path.Contains("register", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("GET", "/api/me", true)]
    [InlineData("GET", "/api/me?x=1", false)]
    [InlineData("GET", "/api/me#x", false)]
    [InlineData("GET", "/api/me/", false)]
    [InlineData("GET", "/api/../api/me", false)]
    [InlineData("POST", "/api/me", false)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/register", false)]
    [InlineData("DELETE", "/api/admin/api-key", false)]
    public void Allowed_is_an_exact_method_and_path_match_with_no_query(string method, string path, bool allowed)
        => Assert.Equal(allowed, OnyxProvisioningRoutes.Allowed(new HttpMethod(method), path));

    [Fact]
    public void Provisioning_and_query_allowlists_do_not_use_each_other()
    {
        var sindri = typeof(HttpOnyxProvisioning).Assembly.GetReferencedAssemblies().Select(static a => a.Name).ToList();
        var context = System.Reflection.Assembly.Load("Aw.Context.Infrastructure").GetReferencedAssemblies().Select(static a => a.Name).ToList();

        Assert.DoesNotContain("Aw.Context.Infrastructure", sindri, StringComparer.Ordinal);
        Assert.DoesNotContain("Aw.Sindri.Infrastructure", context, StringComparer.Ordinal);
        Assert.DoesNotContain("Aw.Sindri.Core", context, StringComparer.Ordinal);
        // The query adapter's own list stays exactly five requests; Aw.Context.Tests' OnyxRequests test holds that number.
    }
}
