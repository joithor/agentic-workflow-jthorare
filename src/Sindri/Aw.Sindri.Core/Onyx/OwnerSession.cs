namespace Aw.Sindri.Core.Onyx;

/// <summary>The owner's login: the value of Onyx's session cookie. It lives in memory for the length of one `onyx up` step.</summary>
/// <param name="cookie">The cookie's value.</param>
public sealed class OwnerSession(string cookie) : OnyxCredential, IDisposable
{
    private readonly System.Runtime.CompilerServices.StrongBox<string?> _cookie = new(cookie);

    /// <summary>Gets the cookie's value.</summary>
    /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
    public string Cookie => _cookie.Value ?? throw new ObjectDisposedException(nameof(OwnerSession));

    /// <summary>Drops the cookie.</summary>
    public void Dispose() => _cookie.Value = null;

    /// <summary>Describes the session without its value.</summary>
    /// <returns>A fixed text.</returns>
    public override string ToString() => "OwnerSession";
}
