namespace Aw.Sindri.Core.Secrets;

/// <summary>Finds secret shapes in text with the one pattern set (Aw.Redaction); the gate that vets a push reads it.</summary>
public interface ISecretScan
{
    /// <summary>The offsets at which <paramref name="text"/> holds a secret shape. A scan that cannot finish counts as a hit at offset 0: it fails closed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The first UTF-16 index of each hit, in order; empty when the text is clean.</returns>
    public IReadOnlyList<int> Hits(string text);
}
