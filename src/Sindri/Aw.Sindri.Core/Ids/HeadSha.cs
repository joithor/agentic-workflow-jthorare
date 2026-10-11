using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A commit SHA a transition or a piece of evidence is bound to: 7 to 64 lower-case hex digits.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<HeadSha>))]
public readonly partial struct HeadSha : ITypedId<HeadSha, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A commit sha is 7 to 64 lower-case hex digits");

    [GeneratedRegex("^[0-9a-f]{7,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
