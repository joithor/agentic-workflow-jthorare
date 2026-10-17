using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>IOnyxProvisioning over HTTP to http://127.0.0.1:&lt;port&gt; only, through the seven-route allowlist.</summary>
/// <param name="client">The client.</param>
internal sealed class HttpOnyxProvisioning(HttpClient client) : IOnyxProvisioning
{
    /// <summary>Builds the client production uses.</summary>
    /// <returns>The client.</returns>
    internal static HttpClient CreateClient() => HttpOnyxHealth.CreateClient();

    /// <summary>Sends one allowlisted request.</summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="method">The method.</param>
    /// <param name="pathAndQuery">The path and query.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status code.</returns>
    internal Task<int> Send(int port, HttpMethod method, string pathAndQuery, CancellationToken cancellationToken)
    {
        _ = (client, port, method, pathAndQuery, cancellationToken);
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public ValueTask<Result<OwnerSession>> Login(int port, string email, string password, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<OnyxAccount>> Me(int port, OnyxCredential credential, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<OnyxMintedKey>> MintAdminKey(int port, OwnerSession session, string name, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Logout(int port, OwnerSession session, CancellationToken cancellationToken) => throw new NotSupportedException();
}
