using System.Globalization;
using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Core.Recipes;

/// <summary>
/// The input check (§17.28.5: every block's inputs are produced upstream): a must-analysis over the state graph. A
/// fact is available at a state when every path from the initial state to it passes a step that outputs it, with the
/// same type. A step's inputs must be available when its state is entered; a gate's or effect's inputs when its
/// transition leaves (after the leaving state's own step).
/// </summary>
internal static class RecipeDataflow
{
    internal static IEnumerable<RecipeProblem> Check(Recipe recipe, IBlockRegistry blocks)
    {
        var entering = Entering(recipe, blocks);
        return Steps(recipe, blocks, entering).Concat(Transitions(recipe, blocks, entering));
    }

    private static IEnumerable<RecipeProblem> Steps(Recipe recipe, IBlockRegistry blocks, Dictionary<string, Dictionary<string, PortType>> entering)
    {
        foreach (var (name, state) in recipe.States.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            if (state.Step is { } step && entering.TryGetValue(name, out var before) && blocks.Find(step) is { } spec)
            {
                foreach (var problem in Needs(spec, before, $"states.{name}.step", name))
                {
                    yield return problem;
                }
            }
        }
    }

    private static IEnumerable<RecipeProblem> Transitions(Recipe recipe, IBlockRegistry blocks, Dictionary<string, Dictionary<string, PortType>> entering)
    {
        foreach (var (t, i) in recipe.Transitions.Select((t, i) => (t, i)))
        {
            if (!entering.TryGetValue(t.From, out var before))
            {
                continue;
            }

            var leaving = Leaving(recipe, t.From, before, blocks);
            var named = (t.Gates ?? []).Concat(t.Effects ?? []).Select(id => blocks.Find(id)).OfType<BlockSpec>();
            foreach (var problem in named.SelectMany(spec => Needs(spec, leaving, string.Create(CultureInfo.InvariantCulture, $"transitions[{i}]"), t.From)))
            {
                yield return problem;
            }
        }
    }

    // The facts available on entering each reachable state, by name with their type.
    private static Dictionary<string, Dictionary<string, PortType>> Entering(Recipe recipe, IBlockRegistry blocks)
    {
        var result = new Dictionary<string, Dictionary<string, PortType>>(StringComparer.Ordinal);
        if (!recipe.States.ContainsKey(recipe.Initial))
        {
            return result;
        }

        result[recipe.Initial] = new(StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var t in recipe.Transitions.Where(t => result.ContainsKey(t.From) && recipe.States.ContainsKey(t.To)))
            {
                var available = Leaving(recipe, t.From, result[t.From], blocks);
                changed |= Meet(result, t.To, available, recipe.Initial);
            }
        }

        return result;
    }

    // Intersects what reaches 'state' with 'available'; true when that changed what is known about the state.
    private static bool Meet(Dictionary<string, Dictionary<string, PortType>> result, string state, Dictionary<string, PortType> available, string initial)
    {
        if (!result.TryGetValue(state, out var known))
        {
            result[state] = new Dictionary<string, PortType>(available, StringComparer.Ordinal);
            return true;
        }

        if (state == initial)
        {
            return false;   // nothing is produced before the workflow starts
        }

        var lost = known.Where(k => !available.TryGetValue(k.Key, out var type) || type != k.Value).Select(k => k.Key).ToList();
        lost.ForEach(name => known.Remove(name));
        return lost.Count > 0;
    }

    private static Dictionary<string, PortType> Leaving(Recipe recipe, string state, Dictionary<string, PortType> entering, IBlockRegistry blocks)
    {
        var available = new Dictionary<string, PortType>(entering, StringComparer.Ordinal);
        if (recipe.State(state)?.Step is { } step && blocks.Find(step) is { } spec)
        {
            foreach (var output in spec.Outputs)
            {
                available[output.Name] = output.Type;
            }
        }

        return available;
    }

    private static IEnumerable<RecipeProblem> Needs(BlockSpec spec, Dictionary<string, PortType> available, string path, string state)
    {
        foreach (var input in spec.Inputs)
        {
            if (!available.TryGetValue(input.Name, out var type))
            {
                yield return new(RecipeChecks.Input, path, $"'{spec.Id.Value}' needs {input}, which isn't produced on every path to '{state}'");
            }
            else if (type != input.Type)
            {
                yield return new(RecipeChecks.Input, path, $"'{spec.Id.Value}' needs {input}, but it is produced as {new PortSpec(input.Name, type)}");
            }
        }
    }
}
