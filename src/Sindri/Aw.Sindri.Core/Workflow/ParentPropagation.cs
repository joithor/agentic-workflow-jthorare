using Aw.Kernel;
using Aw.Kernel.Events;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Composition (§17.28.1): a child's end, park, escalation, unpark or cancellation reaches its parent as an event in the
/// same commit, never as shared state, and walks up the chain while each parent changes in turn.
/// </summary>
/// <param name="loader">Loads parents with their recipes.</param>
/// <param name="decide">Decides the parent's change.</param>
/// <param name="builder">The commit builder.</param>
/// <param name="followUps">Effects and child starts of a parent's transition.</param>
internal sealed class ParentPropagation(InstanceLoader loader, ParentDecisions decide, CommitBuilder builder, FollowUps followUps)
{
    private const int MaxDepth = 8;

    internal async ValueTask<Result<Unit>> Propagate(CommitDraft draft, WorkflowInstance child, WorkflowChange change, EventEnvelope<string> cause, CancellationToken ct)
    {
        for (var depth = 0; depth < MaxDepth && child.Parent is { } parentId; depth++)
        {
            var loaded = await loader.Load(parentId, ct).ConfigureAwait(false);
            if (loaded.IsError)
            {
                return loaded.Error;
            }

            var decided = await decide.For(loaded.Value, child, change, ct).ConfigureAwait(false);
            if (decided is not { } parent)
            {
                return Unit.Value;
            }

            var applied = builder.Record(draft, loaded.Value.Instance, parent.Change, parent.Key, cause);
            if (parent.Transition is { } transition)
            {
                var follow = await followUps.After(draft, loaded.Value.Recipe.Recipe, transition, applied, ct).ConfigureAwait(false);
                if (follow.IsError)
                {
                    return follow.Error;
                }
            }

            (child, change, cause) = (applied.After, parent.Change, applied.Event);
        }

        return Unit.Value;
    }
}
