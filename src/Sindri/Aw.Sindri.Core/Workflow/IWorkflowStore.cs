using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// Workflow persistence (port; factory addendum §17.20, §17.28.1): instance rows and their events, written into the
/// ledger. A commit is one transaction that checks the writer's lease epoch (fencing), the first event's idempotency
/// key, and each instance's version, then records the events, the rows and the outbox commands together. SQLite today;
/// the server store later, passing the same contract suite.
/// </summary>
public interface IWorkflowStore
{
    /// <summary>Commits <paramref name="commit"/> if the grant is current, its key is new and every version matches.</summary>
    /// <param name="commit">The writes and commands.</param>
    /// <param name="grant">The writer's lease grant.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The outcome, SND-LEASE-003 for a stale epoch, or SND-WF-003 when an instance changed.</returns>
    public ValueTask<Result<CommitOutcome>> Commit(WorkflowCommit commit, LeaseGrant grant, CancellationToken cancellationToken);

    /// <summary>The instance <paramref name="id"/>, or null.</summary>
    /// <param name="id">The instance id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The instance.</returns>
    public ValueTask<Result<WorkflowInstance?>> Get(InstanceId id, CancellationToken cancellationToken);

    /// <summary>Instances, newest first.</summary>
    /// <param name="query">Which.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The instances.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> List(InstanceQuery query, CancellationToken cancellationToken);

    /// <summary>Running instances whose timer is due at <paramref name="now"/>, earliest first.</summary>
    /// <param name="now">The time.</param>
    /// <param name="max">The most to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The instances.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Due(DateTimeOffset now, int max, CancellationToken cancellationToken);

    /// <summary>The children of <paramref name="parent"/>, oldest first.</summary>
    /// <param name="parent">The parent instance.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The children.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Children(InstanceId parent, CancellationToken cancellationToken);

    /// <summary>The events of <paramref name="id"/>, oldest first: what replay folds.</summary>
    /// <param name="id">The instance id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The events.</returns>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> History(InstanceId id, CancellationToken cancellationToken);
}
