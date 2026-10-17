using System.Globalization;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The pages of the Onyx on this machine the owner is sent to.</summary>
public static class OnyxPages
{
    /// <summary>The sign-up page, where the first account becomes the admin.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <returns>http://127.0.0.1:&lt;port&gt;/auth/create-account.</returns>
    public static Uri SignUp(int port) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/auth/create-account"));

    /// <summary>The connectors page, where the owner adds Linear and Slack.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <returns>http://127.0.0.1:&lt;port&gt;/admin/connectors.</returns>
    public static Uri Connectors(int port) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/admin/connectors"));
}
