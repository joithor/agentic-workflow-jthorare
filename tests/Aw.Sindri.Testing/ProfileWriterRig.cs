using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IProfileWriter"/> under test and a way to set and read the profile text it works on.</summary>
public abstract class ProfileWriterRig : IDisposable
{
    /// <summary>Gets the writer under test.</summary>
    public abstract IProfileWriter Writer { get; }

    /// <summary>Gets or sets the profile text; null is no file.</summary>
    public abstract string? Text { get; set; }

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
