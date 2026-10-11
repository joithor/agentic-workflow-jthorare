using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>What follows an applied transition in the same commit: its effects as outbox commands, and the child workflow its new state starts.</summary>
/// <param name="builder">The commit builder.</param>
/// <param name="catalog">The recipe catalog (for a child's recipe).</param>
internal sealed class FollowUps(CommitBuilder builder, IRecipeCatalog catalog)
{
    internal async ValueTask<Result<Unit>> After(CommitDraft draft, Recipe recipe, RecipeTransition transition, (WorkflowInstance Instance, EventEnvelope<string> Event) applied,
        CancellationToken ct)
    {
        var effects = builder.Effects(draft, recipe, transition, applied.Instance, applied.Event);
        if (effects.IsError || recipe.State(transition.To)?.Child is not { } child)
        {
            return effects;
        }

        var childRecipe = await catalog.Get(child, ct).ConfigureAwait(false);
        if (childRecipe.IsError)
        {
            return childRecipe.Error;
        }

        var parent = applied.Instance;
        var attempt = (int)Math.Min(parent.Version, int.MaxValue);
        var start = builder.Start(new StartSpec(childRecipe.Value, parent.Item, attempt, parent.Id, parent.BaseSha, parent.Root));
        _ = builder.Record(draft, before: null, start, WorkflowKeys.Start(start.Instance), applied.Event);
        return Unit.Value;
    }
}
