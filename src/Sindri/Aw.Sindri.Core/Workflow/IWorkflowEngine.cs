using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The workflow engine (factory addendum §17.28): starts instances of recipes and applies every change to them as one
/// fenced, idempotent, versioned ledger event with its outbox commands, under the lease this process holds.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>Starts <paramref name="recipe"/> for <paramref name="item"/>; starting the same (recipe, item, attempt) again is a no-op.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="item">The work item.</param>
    /// <param name="attempt">The attempt, from 1.</param>
    /// <param name="baseSha">The base commit, if known.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The instance, or SND-WF-002 / SND-API-001 / SND-LEASE-003.</returns>
    public ValueTask<Result<WorkflowOutcome>> Start(RecipeId recipe, ItemId item, int attempt, HeadSha? baseSha, CancellationToken cancellationToken);

    /// <summary>Applies a signal: the first candidate transition whose gates pass, or a park in its place, or an escalation from a top-rung gate.</summary>
    /// <param name="id">The instance.</param>
    /// <param name="signal">The signal.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outcome, or SND-WF-003/004/005/006/007/008/009.</returns>
    public ValueTask<Result<WorkflowOutcome>> Signal(InstanceId id, WorkflowSignal signal, CancellationToken cancellationToken);

    /// <summary>Applies a universal command: cancel, park, unpark, take over, hand back or register evidence.</summary>
    /// <param name="id">The instance.</param>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outcome, or SND-WF-004/005/008.</returns>
    public ValueTask<Result<WorkflowOutcome>> Apply(InstanceId id, WorkflowCommand command, CancellationToken cancellationToken);

    /// <summary>Fires due timers (after moving them by <paramref name="clockJump"/> when the clock jumped).</summary>
    /// <param name="clockJump">The jump since the last tick, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What it did.</returns>
    public ValueTask<Result<TickReport>> Tick(TimeSpan? clockJump, CancellationToken cancellationToken);
}
