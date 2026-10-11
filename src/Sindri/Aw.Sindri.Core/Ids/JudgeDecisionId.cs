using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A decision the judge recorded, for 'judge why': 1 to 128 characters of letters, digits and _ -.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<JudgeDecisionId>))]
public readonly partial struct JudgeDecisionId : ITypedId<JudgeDecisionId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A judge decision id is 1 to 128 characters: letters, digits, _ and -");

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
