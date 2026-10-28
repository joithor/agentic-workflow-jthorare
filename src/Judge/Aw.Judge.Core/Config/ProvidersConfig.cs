using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Config;

/// <summary>The optional provider selection in config.json.</summary>
/// <param name="AgentClis">Which agent CLIs to use, in priority order (overrides AW_PROVIDER); [] for none; null for the default. Known names only.</param>
/// <param name="Jev">False drops Jev from every chain; null for the default (included).</param>
/// <param name="JevInputCapChars">The cap on the state and instructions Jev is sent, in characters (<see cref="MinJevInputCapChars"/> to <see cref="MaxJevInputCapChars"/>); null for <see cref="DefaultJevInputCapChars"/>.</param>
public sealed record ProvidersConfig(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? AgentClis = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Jev = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? JevInputCapChars = null)
{
    /// <summary>
    /// Jev's wire cap when neither the question nor config.json sets one. Jev's window is 64k tokens; JSON and code run at
    /// about 3 characters a token, so 64k tokens is ~196,000 characters, and ~10k tokens (~30,000 characters) are left for
    /// the instructions, the criteria and the answer: 160,000 characters of state.
    /// </summary>
    public const int DefaultJevInputCapChars = 160_000;

    /// <summary>The smallest "providers.jevInputCapChars" config.json may set.</summary>
    public const int MinJevInputCapChars = 1_000;

    /// <summary>The largest "providers.jevInputCapChars" config.json may set.</summary>
    public const int MaxJevInputCapChars = 256_000;
}
