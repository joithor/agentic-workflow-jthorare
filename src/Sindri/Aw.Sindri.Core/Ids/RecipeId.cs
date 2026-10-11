using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A recipe's id (factory addendum §17.28.1): dotted lower-case words, for example 'workflow.item', at most 64 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecipeId>))]
public readonly partial struct RecipeId : ITypedId<RecipeId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A recipe id is dotted lower-case words, for example workflow.item, at most 64 characters");

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z0-9][a-z0-9-]*)+\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
