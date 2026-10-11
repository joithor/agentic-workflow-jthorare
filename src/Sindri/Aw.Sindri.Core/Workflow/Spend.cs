namespace Aw.Sindri.Core.Workflow;

/// <summary>Tokens and turns spent (or granted).</summary>
/// <param name="Tokens">Tokens.</param>
/// <param name="Turns">Turns.</param>
public sealed record Spend(long Tokens, int Turns)
{
    /// <summary>Nothing.</summary>
    public static Spend Zero { get; } = new(0, 0);

    /// <summary>The sum of this and <paramref name="other"/>.</summary>
    /// <param name="other">The other spend.</param>
    /// <returns>The sum.</returns>
    public Spend Plus(Spend other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(Tokens + other.Tokens, Turns + other.Turns);
    }
}
