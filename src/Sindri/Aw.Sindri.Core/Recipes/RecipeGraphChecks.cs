namespace Aw.Sindri.Core.Recipes;

/// <summary>The graph checks: every state reachable, every non-terminal state has a way out and a timeout, every loop capped and every cycle counted by a loop.</summary>
internal static class RecipeGraphChecks
{
    internal const int MaxLoopCap = 50;

    internal static IEnumerable<RecipeProblem> Check(Recipe recipe)
    {
        var edges = Edges(recipe, withLoops: true);
        return States(recipe, edges, Reach(recipe.Initial, edges)).Concat(Loops(recipe));
    }

    private static IEnumerable<RecipeProblem> States(Recipe recipe, Dictionary<string, List<string>> edges, HashSet<string> reachable)
    {
        foreach (var (name, state) in recipe.States.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            if (!reachable.Contains(name))
            {
                yield return new(RecipeChecks.Reachable, $"states.{name}", $"no transition path leads here from '{recipe.Initial}'");
            }

            if (state.Kind is not StateKind.Terminal && !edges.ContainsKey(name))
            {
                yield return new(RecipeChecks.Exit, $"states.{name}", "a non-terminal state needs a transition out");
            }

            if (state.Kind is not StateKind.Terminal && RecipeDuration.Parse(state.Timeout) is null)
            {
                yield return new(RecipeChecks.Timeout, $"states.{name}.timeout",
                    state.Timeout is null ? "an active or wait state needs a timeout" : $"'{state.Timeout}' is not a duration: a whole number and s, m, h or d, from 1s to 30d");
            }
        }
    }

    private static IEnumerable<RecipeProblem> Loops(Recipe recipe)
    {
        foreach (var (name, loop) in (recipe.Loops ?? new Dictionary<string, LoopSpec>(StringComparer.Ordinal)).OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            if (loop.Cap is < 1 or > MaxLoopCap)
            {
                yield return new(RecipeChecks.Loop, $"loops.{name}.cap", $"a loop's cap is 1 to {MaxLoopCap}");
            }
        }

        if (Cycle(Edges(recipe, withLoops: false)) is { } cycle)
        {
            yield return new(RecipeChecks.Loop, "transitions", $"the states {string.Join(" -> ", cycle)} form a cycle no loop counts: put 'loop: <name>' on one of its transitions");
        }
    }

    private static Dictionary<string, List<string>> Edges(Recipe recipe, bool withLoops)
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var t in recipe.Transitions.Where(t => recipe.States.ContainsKey(t.From) && recipe.States.ContainsKey(t.To) && (withLoops || t.Loop is null)))
        {
            if (!edges.TryGetValue(t.From, out var to))
            {
                edges[t.From] = to = [];
            }

            to.Add(t.To);
        }

        return edges;
    }

    private static HashSet<string> Reach(string initial, Dictionary<string, List<string>> edges)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { initial };
        var queue = new Queue<string>([initial]);
        while (queue.TryDequeue(out var state))
        {
            foreach (var next in edges.GetValueOrDefault(state) ?? [])
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    // A depth-first search for a back edge; returns the cycle's states, first one repeated at the end.
    private static List<string>? Cycle(Dictionary<string, List<string>> edges)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in edges.Keys.Order(StringComparer.Ordinal))
        {
            var path = new List<string>();
            if (Visit(start, edges, path, done) is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }

    private static List<string>? Visit(string state, Dictionary<string, List<string>> edges, List<string> path, HashSet<string> done)
    {
        if (path.IndexOf(state) is var at and >= 0)
        {
            return [.. path.Skip(at), state];
        }

        if (!done.Add(state))
        {
            return null;
        }

        path.Add(state);
        foreach (var next in edges.GetValueOrDefault(state) ?? [])
        {
            if (Visit(next, edges, path, done) is { } cycle)
            {
                return cycle;
            }
        }

        path.RemoveAt(path.Count - 1);
        return null;
    }
}
