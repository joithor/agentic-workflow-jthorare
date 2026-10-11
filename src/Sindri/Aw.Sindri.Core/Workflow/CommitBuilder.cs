using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Turns decided changes into a commit: each change becomes a ledger event (keyed, in the instance's correlation, or
/// caused by the event that propagated it) and the folded instance row; a transition's effects become outbox commands
/// caused by its event; entering a state with a child starts the child in the same commit.
/// </summary>
/// <param name="events">The event factory.</param>
/// <param name="blocks">The block registry.</param>
/// <param name="scope">The tenant and ring new instances carry.</param>
/// <param name="clock">The clock new instances start by.</param>
internal sealed class CommitBuilder(EventFactory events, IBlockRegistry blocks, EventScope scope, TimeProvider clock)
{
    internal (WorkflowInstance After, EventEnvelope<string> Event) Record(CommitDraft draft, WorkflowInstance? before, WorkflowChange change, string key,
        EventEnvelope<string>? cause = null)
    {
        var after = InstanceFold.Apply(before, change);
        var entry = cause is null
            ? events.Correlated(WorkflowKeys.Correlation(after.Root), change.EventType, change, key, after.Item)
            : events.Caused(cause, change.EventType, change, key, after.Item);
        draft.Writes.Add(new InstanceWrite(entry, after, before?.Version ?? 0));
        return (after, entry.Envelope);
    }

    internal WorkflowChange Start(StartSpec spec)
    {
        var at = clock.GetUtcNow();
        var recipe = spec.Recipe.Recipe;
        var id = spec.Parent is { } parent ? WorkflowKeys.Child(parent, recipe.Id, spec.Attempt) : WorkflowKeys.Instance(scope.Tenant, recipe.Id, spec.Item, spec.Attempt);
        var empty = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var instance = new WorkflowInstance(id, recipe.Id, spec.Recipe.Hash, spec.Item, spec.Attempt, spec.Parent, spec.Root ?? id, scope.Tenant,
            scope.Ring,
            Version: 1, recipe.Initial, Hold: null, TakenOver: false, Ended: null, empty, new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), Spend.Zero, Spend.Zero, Spend.Zero, [],
            Deadlines.For(recipe.State(recipe.Initial), at), Paused: null, Retries: 0, spec.BaseSha, at, at, at);
        return new WorkflowChange(id, ChangeKind.Started, 1, at, Start: instance, Deadline: instance.Deadline, Child: spec.Parent is null ? null : id);
    }

    internal Result<Unit> Effects(CommitDraft draft, Recipe recipe, RecipeTransition transition, WorkflowInstance after, EventEnvelope<string> cause)
    {
        foreach (var id in transition.Effects ?? [])
        {
            if (blocks.Effect(id) is not { } effect)
            {
                return SindriErrors.RecipeInvalid.With($"effect '{id.Value}' isn't registered");
            }

            var command = effect.Command(new EffectContext(after.Id, after.Item, recipe.Id, transition.From, transition.To, after.Facts));
            if (command.IsError)
            {
                return command.Error;
            }

            draft.Commands.Add(events.Command(cause, command.Value.Kind, command.Value.PayloadJson));
        }

        return Unit.Value;
    }
}
