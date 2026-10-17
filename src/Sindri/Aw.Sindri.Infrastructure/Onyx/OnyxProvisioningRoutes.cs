using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// The only requests the provisioning client may send (spec §5.2): seven routes. The owner's login and the admin key are powerful, so a request off this list
/// is refused before it is sent. There is no registration route (spec O3) and no way to change or delete a key. This list is separate from the query adapter's.
/// </summary>
internal static class OnyxProvisioningRoutes
{
    /// <summary>Gets the seven allowed requests.</summary>
    public static IReadOnlyList<(HttpMethod Method, string Path)> Allowlist { get; } =
    [
        (HttpMethod.Get, OnyxRoutes.Health),
        (HttpMethod.Get, OnyxRoutes.Version),
        (HttpMethod.Post, "/api/auth/login"),
        (HttpMethod.Post, "/api/auth/logout"),
        (HttpMethod.Get, "/api/me"),
        (HttpMethod.Get, "/api/manage/users/accepted/all"),
        (HttpMethod.Post, "/api/admin/api-key"),
    ];

    /// <summary>Whether a request is on the list: an exact path, no query, no fragment.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The path and query as sent.</param>
    /// <returns>True when the method and path are listed.</returns>
    public static bool Allowed(HttpMethod method, string pathAndQuery)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(pathAndQuery);
        return Allowlist.Any(r => r.Method == method && string.Equals(r.Path, pathAndQuery, StringComparison.Ordinal));
    }
}
