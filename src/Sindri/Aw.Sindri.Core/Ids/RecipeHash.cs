using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A recipe's hash: 64 lower-case hex digits of SHA-256 over its canonical JSON. An instance is pinned to the hash it started with.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecipeHash>))]
public readonly partial struct RecipeHash : ITypedId<RecipeHash, string>
{
    /// <summary>The first eight digits, for listings.</summary>
    public string Abbreviated => Value[..8];

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A recipe hash is 64 lower-case hex digits");

    [GeneratedRegex("^[0-9a-f]{64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
