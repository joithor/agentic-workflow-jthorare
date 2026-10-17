using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// What `onyx up` asks of the Onyx it started, to turn the owner's login into Sindri's own admin service-account key. It has its own request allowlist
/// (<see cref="OnyxProvisioningRoutes"/>), never the query adapter's. No call registers an account (spec O3). None of the secrets it handles (password,
/// cookie, key) appears in an error or a description.
/// </summary>
public interface IOnyxProvisioning
{
    /// <summary>Logs the owner in with `POST /api/auth/login`.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <param name="email">The owner's email.</param>
    /// <param name="password">The owner's password.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session; or SND-ONYX-008 (wrong credentials, a locked-out account, an unexpected answer).</returns>
    public ValueTask<Result<OwnerSession>> Login(int port, string email, string password, CancellationToken cancellationToken);

    /// <summary>Asks `GET /api/me` who the credential is.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <param name="credential">The owner's session or an API key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account; or SND-ONYX-008 when Onyx refuses the credential or answers something unexpected.</returns>
    public ValueTask<Result<OnyxAccount>> Me(int port, OnyxCredential credential, CancellationToken cancellationToken);

    /// <summary>Creates an API key that belongs to Onyx's `Admin` group, as the logged-in owner.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <param name="session">The owner's session.</param>
    /// <param name="name">The key's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The key; SND-ONYX-007 when the owner isn't an admin; SND-ONYX-008 for anything else that goes wrong.</returns>
    public ValueTask<Result<OnyxMintedKey>> MintAdminKey(int port, OwnerSession session, string name, CancellationToken cancellationToken);

    /// <summary>Logs the owner out with `POST /api/auth/logout`.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <param name="session">The owner's session.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or SND-ONYX-008 (the caller warns and goes on).</returns>
    public ValueTask<Result<Unit>> Logout(int port, OwnerSession session, CancellationToken cancellationToken);
}
