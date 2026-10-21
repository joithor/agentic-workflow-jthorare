using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Testing.Forge;

/// <summary>An <see cref="ISecretScan"/> that takes the marker 'PLANTED-SECRET' for a secret, wherever it stands.</summary>
public sealed class FakeSecretScan : ISecretScan
{
    /// <summary>The text the fake takes for a secret.</summary>
    public const string Marker = "PLANTED-SECRET";

    /// <inheritdoc/>
    public IReadOnlyList<int> Hits(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<int> hits = [];
        for (var at = text.IndexOf(Marker, StringComparison.Ordinal); at >= 0; at = text.IndexOf(Marker, at + Marker.Length, StringComparison.Ordinal))
        {
            hits.Add(at);
        }

        return hits;
    }
}
