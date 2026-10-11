using System.Text.RegularExpressions;

namespace Aw.Sindri.Infrastructure.Recipes;

/// <summary>Turns AwJson's message for a recipe that doesn't fit the schema into one a recipe author can act on: the key, or the path and what went wrong, without JSON byte positions.</summary>
internal static partial class RecipeErrors
{
    internal static string Explain(string message)
    {
        if (Unmapped.Match(message) is { Success: true } unmapped)
        {
            var type = unmapped.Groups["type"].Value;
            return $"'{unmapped.Groups["name"].Value}' is not a key of {Word(type[(type.LastIndexOf('.') + 1)..])} (keys are in docs/csharp/sindri-engine.md)";
        }

        if (Missing.Match(message) is { Success: true } missing)
        {
            return $"{missing.Groups["names"].Value.Replace("'", string.Empty, StringComparison.Ordinal)}: required";
        }

        var at = PathPart.Match(message) is { Success: true } where ? where.Groups["path"].Value.TrimStart('$').TrimStart('.') : string.Empty;
        if (Refused.Match(message) is { Success: true } refused && at.Length > 0)
        {
            return $"{at}: not a valid {Spaced(refused.Groups["type"].Value)}";
        }

        var path = at.Length > 0 ? at + ": " : string.Empty;
        return path + PathPart.Replace(Positions.Replace(message, string.Empty), string.Empty).Trim();
    }

    private static string Spaced(string type) => Words.Replace(type, " $0").Trim().ToLowerInvariant();

    private static string Word(string type) => type switch
    {
        "Recipe" => "a recipe",
        "RecipeState" => "a state",
        "RecipeTransition" => "a transition",
        "LoopSpec" => "a loop",
        "BudgetSpec" => "a budget",
        _ => type,
    };

    [GeneratedRegex(@"The JSON property '(?<name>[^']+)' could not be mapped to any \.NET member contained in type '(?<type>[^']+)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Unmapped { get; }

    [GeneratedRegex(@"missing required properties including: (?<names>.+?)\.?$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Missing { get; }

    [GeneratedRegex(@"The value is not a valid (?<type>[A-Za-z]+)\.", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Refused { get; }

    [GeneratedRegex("[A-Z]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words { get; }

    [GeneratedRegex(@" ?Path: (?<path>\$[^ |]*)", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PathPart { get; }

    [GeneratedRegex(@" ?\| LineNumber: \d+ \| BytePositionInLine: \d+\.?", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Positions { get; }
}
