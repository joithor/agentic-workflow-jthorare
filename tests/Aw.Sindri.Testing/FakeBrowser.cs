using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An IBrowser that remembers what it was asked to open.</summary>
public sealed class FakeBrowser : IBrowser
{
    /// <summary>Gets the addresses opened, in order.</summary>
    public IList<Uri> Opened { get; } = [];

    /// <summary>Gets or sets what Open returns (false: no browser tool).</summary>
    public bool Result { get; set; } = true;

    /// <inheritdoc />
    public bool Open(Uri url)
    {
        Opened.Add(url);
        return Result;
    }
}
