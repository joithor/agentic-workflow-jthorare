using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="ISecretWriter"/> under test and a way to read back what it stored.</summary>
public abstract class SecretWriterRig : IDisposable
{
    /// <summary>Gets the writer under test.</summary>
    public abstract ISecretWriter Writer { get; }

    /// <summary>Reads the value a pointer the writer returned holds, or null when it holds none.</summary>
    /// <param name="secretPointer">The pointer.</param>
    /// <returns>The value.</returns>
    public abstract string? Read(SecretPointer secretPointer);

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
