using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IOnyxProvisioning"/> under test, the owner it knows and the port to call.</summary>
public abstract class ProvisioningRig : IDisposable
{
    /// <summary>Gets the client under test.</summary>
    public abstract IOnyxProvisioning Client { get; }

    /// <summary>Gets the port to call.</summary>
    public virtual int Port => 3000;

    /// <summary>Gets the owner's email.</summary>
    public abstract string Email { get; }

    /// <summary>Gets the owner's password.</summary>
    public abstract string Password { get; }

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
