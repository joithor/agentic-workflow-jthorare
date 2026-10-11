using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Infrastructure.Profile;

namespace Aw.Sindri.Infrastructure.Recipes;

/// <summary>
/// YAML text to a recipe: <see cref="YamlToJson"/> (plain data: no anchors, aliases, tags or duplicate keys), then AwJson
/// into <see cref="Recipe"/> (unknown keys and missing required ones refused: the schema check), then the hash over the
/// canonical JSON.
/// </summary>
internal static class RecipeReading
{
    internal static RecipeRead Read(string yaml, JsonSerializerOptions options)
    {
        if (YamlToJson.TryConvert(yaml, out var json) is { } problem)
        {
            return Schema(problem);
        }

        var typed = AwJson.Deserialize<Recipe>(json, options);
        if (typed.IsError)
        {
            return Schema(RecipeErrors.Explain(typed.Error.Message));
        }

        var hash = RecipeHashing.Of(AwJson.Serialize(typed.Value.Canonical(), options));
        return new RecipeRead(typed.Value, hash, Problem: null);
    }

    private static RecipeRead Schema(string message) => new(Recipe: null, Hash: null, new RecipeProblem(RecipeChecks.Schema, "$", message));
}
