using System.Globalization;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>The block checks: every named block is registered with the right kind, capability blocks only in approved recipes, effects on the allowlist, evidence kinds known.</summary>
internal static class RecipeBlockChecks
{
    internal static IEnumerable<RecipeProblem> Check(Recipe recipe, bool approved, IBlockRegistry blocks)
    {
        var uses = recipe.States.Where(s => s.Value.Step is not null)
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => (Path: $"states.{s.Key}.step", Id: s.Value.Step!.Value, Kind: BlockKind.Step))   // why: filtered on Step above
            .Concat(recipe.Transitions.SelectMany(Uses));
        foreach (var (path, id, kind) in uses)
        {
            foreach (var problem in Use(path, id, kind, approved, blocks))
            {
                yield return problem;
            }
        }

        foreach (var problem in recipe.Transitions.SelectMany((t, i) => Evidence(t, string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].evidence"), blocks)))
        {
            yield return problem;
        }
    }

    /// <summary>The evidence kind a requirement names ('test' for 'test@head') and the sha it binds to (head, base, or null for any).</summary>
    /// <param name="requirement">The requirement text.</param>
    /// <returns>The kind and binding, or null when the text isn't a requirement.</returns>
    internal static (string Kind, string? Binding)? Requirement(string requirement)
    {
        var parts = requirement.Split('@');
        return parts switch
        {
            [var kind] when kind.Length > 0 => (kind, null),
            [var kind, "head" or "base"] when kind.Length > 0 => (kind, parts[1]),
            _ => null,
        };
    }

    private static IEnumerable<(string Path, BlockId Id, BlockKind Kind)> Uses(RecipeTransition t, int i) =>
        (t.Gates ?? []).Select((g, j) => (string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].gates[{j}]"), g, BlockKind.Gate))
            .Concat((t.Effects ?? []).Select((e, j) => (string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].effects[{j}]"), e, BlockKind.Effect)));

    private static IEnumerable<RecipeProblem> Use(string path, BlockId id, BlockKind kind, bool approved, IBlockRegistry blocks)
    {
        if (blocks.Find(id) is not { } spec)
        {
            yield return new(RecipeChecks.Block, path, $"block '{id.Value}' isn't registered");
            yield break;
        }

        if (spec.Kind != kind)
        {
            yield return new(RecipeChecks.Block, path, $"'{id.Value}' is a {Word(spec.Kind)} block; a {Word(kind)} block goes here");
            yield break;
        }

        if (spec.Capability && !approved)
        {
            yield return new(RecipeChecks.Capability, path, $"'{id.Value}' bears a capability, so only an approved recipe may name it");
        }

        if (kind is BlockKind.Effect && (spec.CommandKind is null || !EffectAllowlist.Kinds.Contains(spec.CommandKind)))
        {
            yield return new(RecipeChecks.Effect, path, $"'{id.Value}' emits '{spec.CommandKind}', which isn't an allowlisted command kind ({string.Join(", ", EffectAllowlist.Kinds.Order(StringComparer.Ordinal))})");
        }
    }

    private static IEnumerable<RecipeProblem> Evidence(RecipeTransition t, string path, IBlockRegistry blocks)
    {
        foreach (var requirement in t.Evidence ?? [])
        {
            if (Requirement(requirement) is not { } parsed)
            {
                yield return new(RecipeChecks.Block, path, $"'{requirement}' is not 'kind', 'kind@head' or 'kind@base'");
            }
            else if (!BlockId.TryFrom($"evidence.{parsed.Kind}", out var id) || blocks.Find(id) is not { Kind: BlockKind.Evidence })
            {
                yield return new(RecipeChecks.Block, path, $"evidence kind '{parsed.Kind}' isn't registered (no block evidence.{parsed.Kind})");
            }
        }
    }

    private static string Word(BlockKind kind) => kind.ToString().ToLowerInvariant();
}
