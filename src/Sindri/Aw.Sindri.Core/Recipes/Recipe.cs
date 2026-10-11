using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>
/// A workflow definition (factory addendum §17.28.1): declarative config, read from YAML through AwJson (unknown keys are
/// errors) and checked by <see cref="RecipeValidator"/>. The engine interprets it; new code is only needed for a new block.
/// </summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">The recipe's own version, from 1.</param>
/// <param name="Initial">The state a new instance starts in.</param>
/// <param name="States">The states by name (lower-case kebab).</param>
/// <param name="Transitions">The transitions, in the order they are tried.</param>
/// <param name="Loops">The loop counters by name, each with its cap.</param>
/// <param name="Budget">The instance budget.</param>
/// <param name="Description">One line on what the workflow does.</param>
/// <param name="Hash">A frozen recipe's hash; when set it must match the content (§17.28.5).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Recipe(
    RecipeId Id,
    int Version,
    string Initial,
    IReadOnlyDictionary<string, RecipeState> States,
    IReadOnlyList<RecipeTransition> Transitions,
    IReadOnlyDictionary<string, LoopSpec>? Loops = null,
    BudgetSpec? Budget = null,
    string? Description = null,
    RecipeHash? Hash = null)
{
    /// <summary>The signal a parent receives when its child workflow completes.</summary>
    public const string ChildDone = "child.done";

    /// <summary>The recipe in canonical form, which the hash covers: no hash field, states and loops sorted by name, transitions in order.</summary>
    /// <returns>The canonical recipe.</returns>
    public Recipe Canonical() => this with
    {
        Hash = null,
        States = new SortedDictionary<string, RecipeState>(States.ToDictionary(), StringComparer.Ordinal),
        Loops = Loops is null ? null : new SortedDictionary<string, LoopSpec>(Loops.ToDictionary(), StringComparer.Ordinal),
    };

    /// <summary>The state named <paramref name="name"/>, or null.</summary>
    /// <param name="name">The state name.</param>
    /// <returns>The state.</returns>
    public RecipeState? State(string name) => States.TryGetValue(name, out var state) ? state : null;
}
