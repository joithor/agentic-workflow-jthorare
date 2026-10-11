using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing;

/// <summary>Workflow instances with deterministic defaults, for store contracts and decision tests.</summary>
public static class AnInstance
{
    /// <summary>The default recipe hash.</summary>
    public static RecipeHash Hash { get; } = RecipeHash.From(new string('a', 64));

    /// <summary>An active instance of <paramref name="recipe"/> for <paramref name="item"/> at version 1 in <paramref name="state"/>.</summary>
    /// <param name="item">The item.</param>
    /// <param name="at">When it started.</param>
    /// <param name="state">Its state.</param>
    /// <param name="recipe">Its recipe id.</param>
    /// <returns>The instance.</returns>
    public static WorkflowInstance New(string item, DateTimeOffset at, string state = "work", string recipe = "workflow.test")
    {
        var recipeId = RecipeId.From(recipe);
        var itemId = ItemId.From(item);
        return new WorkflowInstance(WorkflowKeys.Instance("local", recipeId, itemId, 1), recipeId, Hash, itemId, 1, Parent: null, "local", 0, 1, state,
            Hold: null, TakenOver: false, Ended: null, new SortedDictionary<string, string>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal), new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            Spend.Zero, Spend.Zero, Spend.Zero, [], Deadline: null, Paused: null, Retries: 0, BaseSha: null, at, at, at);
    }
}
