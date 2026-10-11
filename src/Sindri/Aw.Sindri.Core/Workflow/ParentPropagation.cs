using Aw.Kernel;
using Aw.Kernel.Events;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Composition (§17.28.1): a child's end, park, escalation, unpark or cancellation reaches its parent as an event in the
/// same commit, never as shared state, and walks up the chain while each parent changes in turn, at most
/// <see cref="MaxDepth"/> levels. A parent the draft already changed is read from the draft, so two changes to one
/// instance in a commit chain their versions.
/// </summary>
/// <param name="loader">Loads parents and their recipes.</param>
/// <param name="decide">Decides the parent's change.</param>
/// <param name="builder">The commit builder.</param>
/// <param name="followUps">Effects and child starts of a parent's transition.</param>
internal sealed class ParentPropagation(InstanceLoader loader, ParentDecisions decide, CommitBuilder builder, FollowUps followUps)
{
    /// <summary>How deep a workflow tree propagates and cascades.</summary>
    internal const int MaxDepth = 8;

    internal async ValueTask<Result<Unit>> Propagate(CommitDraft draft, WorkflowInstance child, WorkflowChange change, EventEnvelope<string> cause, CancellationToken ct)
    {
        for (var depth = 0; depth < MaxDepth && child.Parent is { } parentId; depth++)
        {
            var found = await Current(draft, parentId, ct).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            var parent = found.Value;
            var recipe = await loader.Optional(parent, ct).ConfigureAwait(false);
            if (recipe.IsError)
            {
                return recipe.Error;
            }

            var decided = await decide.For(parent, recipe.Value?.Recipe, child, change, ct).ConfigureAwait(false);
            if (decided is not { } next)
            {
                return Unit.Value;
            }

            var applied = await Apply(draft, (parent, recipe.Value?.Recipe), next, cause, ct).ConfigureAwait(false);
            if (applied.IsError)
            {
                return applied.Error;
            }

            (child, change, cause) = (applied.Value.After, next.Change, applied.Value.Event);
        }

        return Unit.Value;
    }

    private async ValueTask<Result<WorkflowInstance>> Current(CommitDraft draft, Ids.InstanceId id, CancellationToken ct) =>
        draft.Latest(id) is { } drafted ? drafted : await loader.Find(id, ct).ConfigureAwait(false);

    /// <summary>Records <paramref name="next"/> on the target instance with its effects and child start when it is a transition.</summary>
    internal async ValueTask<Result<(WorkflowInstance After, EventEnvelope<string> Event)>> Apply(CommitDraft draft, (WorkflowInstance Instance, Recipes.Recipe? Recipe) target,
        ParentChange next, EventEnvelope<string> cause, CancellationToken ct)
    {
        var (instance, recipe) = target;
        var applied = builder.Record(draft, instance, next.Change, next.Key, cause);
        if (next.Transition is { } transition && recipe is not null)
        {
            var follow = await followUps.After(draft, recipe, transition, applied, ct).ConfigureAwait(false);
            if (follow.IsError)
            {
                return follow.Error;
            }
        }

        return applied;
    }
}
