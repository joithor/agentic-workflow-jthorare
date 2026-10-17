using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>
/// An in-memory IOnyxProvisioning with the same observable behavior as the HTTP client over <see cref="FakeOnyxAdmin"/>, held to <see cref="ProvisioningContract"/>.
/// It records its calls by name (never a secret) and can be scripted per call.
/// </summary>
public sealed class FakeOnyxProvisioning : IOnyxProvisioning
{
    private readonly Dictionary<string, (bool Admin, bool ServiceAccount)> _keys = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the owner's email.</summary>
    public string OwnerEmail { get; set; } = "owner@example.com";

    /// <summary>Gets or sets the owner's password.</summary>
    public string OwnerPassword { get; set; } = "correct horse battery staple";

    /// <summary>Gets or sets a value indicating whether the owner is an admin.</summary>
    public bool OwnerIsAdmin { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the owner shows up in Onyx's `Admin` group.</summary>
    public bool OwnerInAdminGroup { get; set; } = true;

    /// <summary>Gets or sets the raw key the next mint returns.</summary>
    public string MintedKey { get; set; } = "on_minted-key-0123456789abcdef";

    /// <summary>Gets or sets the id the next mint returns.</summary>
    public int MintedKeyId { get; set; } = 41;

    /// <summary>Gets or sets an error every login returns instead of the scripted behavior.</summary>
    public Error? LoginError { get; set; }

    /// <summary>Gets or sets an error every mint returns before it reaches Onyx.</summary>
    public Error? MintError { get; set; }

    /// <summary>Gets or sets an error every logout returns.</summary>
    public Error? LogoutError { get; set; }

    /// <summary>Gets the calls made, in order: `login`, `me:session`, `me:key`, `mint`, `logout`.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets the names the keys were minted under.</summary>
    public IList<string> MintedNames { get; } = [];

    /// <summary>Makes a stored key stop being accepted, as when Onyx was reset.</summary>
    /// <param name="key">The key.</param>
    public void Revoke(string key) => _ = _keys.Remove(key);

    /// <summary>Makes a key valid with the given standing without minting it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="admin">Whether it is an admin key.</param>
    public void Accept(string key, bool admin) => _keys[key] = (admin, true);

    /// <inheritdoc />
    public ValueTask<Result<OwnerSession>> Login(int port, string email, string password, CancellationToken cancellationToken)
    {
        Calls.Add("login");
        if (LoginError is { } forced)
        {
            return Fail<OwnerSession>(forced);
        }

        return email == OwnerEmail && password == OwnerPassword
            ? Ok(new OwnerSession("fake-session-cookie"))
            : Fail<OwnerSession>(SindriErrors.OnyxKeyFailed.With("Onyx didn't accept that email and password"));
    }

    /// <inheritdoc />
    public ValueTask<Result<OnyxAccount>> Me(int port, OnyxCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        switch (credential)
        {
            case OwnerSession:
                Calls.Add("me:session");
                return Ok(new OnyxAccount(OwnerEmail, OwnerIsAdmin, IsServiceAccount: false));
            case ApiKeyCredential key:
                Calls.Add("me:key");
                return _keys.TryGetValue(key.Key, out var standing)
                    ? Ok(new OnyxAccount("api_key__sindri-onyx@onyxapikey.ai", standing.Admin, standing.ServiceAccount))
                    : Fail<OnyxAccount>(SindriErrors.OnyxKeyFailed.With("Onyx didn't accept the key"));
            default:
                throw new ArgumentOutOfRangeException(nameof(credential), credential, "unknown credential");
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<OnyxMintedKey>> MintAdminKey(int port, OwnerSession session, string name, CancellationToken cancellationToken)
    {
        Calls.Add("mint");
        if (MintError is { } forced)
        {
            return Fail<OnyxMintedKey>(forced);
        }

        if (!OwnerIsAdmin || !OwnerInAdminGroup)
        {
            return Fail<OnyxMintedKey>(SindriErrors.OnyxLoginNotAdmin.With("that Onyx account isn't an admin; " + OnyxFixes.LoginNotAdmin));
        }

        MintedNames.Add(name);
        _keys[MintedKey] = (Admin: true, ServiceAccount: true);
        return Ok(new OnyxMintedKey(MintedKey, MintedKeyId));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Logout(int port, OwnerSession session, CancellationToken cancellationToken)
    {
        Calls.Add("logout");
        return LogoutError is { } error ? Fail<Unit>(error) : Ok(Unit.Value);
    }

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult(Results.Success(value));

    private static ValueTask<Result<T>> Fail<T>(Error error) => ValueTask.FromResult(Results.Failure<T>(error));
}
