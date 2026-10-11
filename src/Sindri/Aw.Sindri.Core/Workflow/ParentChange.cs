using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A parent's change propagated from a child: the change, its key, and the recipe transition it applies (for 'child.done').</summary>
/// <param name="Change">The change.</param>
/// <param name="Key">Its idempotency key.</param>
/// <param name="Transition">The transition, when it is one.</param>
internal sealed record ParentChange(WorkflowChange Change, string Key, RecipeTransition? Transition);
