using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>The shape checks: names, the initial state, and that states, transitions, loops and children refer to each other.</summary>
internal static partial class RecipeShapeChecks
{
    internal static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal) { "cancelled", "escalated", "parked", "taken-over" };

    internal static IEnumerable<RecipeProblem> Check(Recipe recipe, IReadOnlySet<RecipeId> knownRecipes)
    {
        if (recipe.Version < 1)
        {
            yield return Problem("version", "the version is a whole number from 1");
        }

        if (recipe.State(recipe.Initial) is not { } initial)
        {
            yield return Problem("initial", $"'{recipe.Initial}' is not a state");
        }
        else if (initial.Kind is StateKind.Terminal)
        {
            yield return Problem("initial", "the initial state can't be terminal");
        }

        foreach (var problem in recipe.States.SelectMany(s => State(recipe, s.Key, s.Value, knownRecipes)))
        {
            yield return problem;
        }

        foreach (var problem in recipe.Transitions.SelectMany((t, i) => Transition(recipe, t, string.Create(CultureInfo.InvariantCulture, $"transitions[{i}]"))))
        {
            yield return problem;
        }
    }

    private static IEnumerable<RecipeProblem> State(Recipe recipe, string name, RecipeState state, IReadOnlySet<RecipeId> knownRecipes)
    {
        var path = $"states.{name}";
        if (!Name.IsMatch(name) || Reserved.Contains(name))
        {
            yield return Problem(path, Reserved.Contains(name) ? $"'{name}' is a universal state every recipe gets" : "a state name is lower-case kebab, at most 40 characters");
        }

        var problem = (state.Kind, state) switch
        {
            (StateKind.Active, { Step: null }) => "an active state names its step block",
            (not StateKind.Active, { Step: not null }) => "only an active state names a step",
            (not StateKind.Wait, { Child: not null }) => "only a wait state starts a child workflow",
            (StateKind.Terminal, _) when state.Timeout is not null || state.OnTimeout is not null || state.Budget is not null => "a terminal state has no timeout or budget",
            _ => null,
        };
        if (problem is not null)
        {
            yield return Problem(path, problem);
        }

        foreach (var child in Child(recipe, name, state, knownRecipes))
        {
            yield return Problem($"{path}.child", child);
        }
    }

    private static IEnumerable<string> Child(Recipe recipe, string name, RecipeState state, IReadOnlySet<RecipeId> knownRecipes)
    {
        var waits = recipe.Transitions.Any(t => t.From == name && t.On == Recipe.ChildDone);
        if (state.Child is not { } child)
        {
            if (waits)
            {
                yield return $"'{Recipe.ChildDone}' only leaves a state that starts a child";
            }

            yield break;
        }

        if (child == recipe.Id || !knownRecipes.Contains(child))
        {
            yield return child == recipe.Id ? "a recipe can't start itself as a child" : $"child recipe '{child.Value}' isn't registered";
        }

        if (!waits)
        {
            yield return $"a state with a child needs a transition on '{Recipe.ChildDone}'";
        }
    }

    private static IEnumerable<RecipeProblem> Transition(Recipe recipe, RecipeTransition t, string path)
    {
        if (recipe.State(t.From) is not { } from)
        {
            yield return Problem($"{path}.from", $"'{t.From}' is not a state");
        }
        else if (from.Kind is StateKind.Terminal)
        {
            yield return Problem($"{path}.from", $"'{t.From}' is terminal, so nothing leaves it");
        }

        if (recipe.State(t.To) is null)
        {
            yield return Problem($"{path}.to", $"'{t.To}' is not a state");
        }

        if (!Signal.IsMatch(t.On))
        {
            yield return Problem($"{path}.on", "a signal is lower-case words with . or -, at most 40 characters");
        }

        if (t.Loop is { } loop && recipe.Loops?.ContainsKey(loop) != true)
        {
            yield return Problem($"{path}.loop", $"'{loop}' is not in loops");
        }
    }

    private static RecipeProblem Problem(string path, string message) => new(RecipeChecks.Shape, path, message);

    [GeneratedRegex("^[a-z][a-z0-9-]{0,39}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Name { get; }

    [GeneratedRegex("^[a-z][a-z0-9.-]{0,39}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Signal { get; }
}
