using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Outbox;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory workflow store, fenced by <paramref name="lease"/>; passes <see cref="WorkflowStoreContract"/>.</summary>
/// <param name="lease">The lease every commit is fenced by.</param>
public sealed class InMemoryWorkflowStore(ILease lease) : IWorkflowStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<InstanceId, WorkflowInstance> _instances = [];
    private readonly List<(InstanceId Instance, LedgerEvent Event)> _events = [];
    private readonly List<OutboxCommand> _commands = [];

    /// <summary>The outbox commands committed so far.</summary>
    public IReadOnlyList<OutboxCommand> Commands { get { lock (_gate) { return [.. _commands]; } } }

    /// <summary>Every workflow event committed so far, oldest first.</summary>
    public IReadOnlyList<LedgerEvent> Events { get { lock (_gate) { return [.. _events.Select(e => e.Event)]; } } }

    /// <inheritdoc/>
    public async ValueTask<Result<CommitOutcome>> Commit(WorkflowCommit commit, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commit);
        ArgumentNullException.ThrowIfNull(grant);
        var row = await lease.Read(cancellationToken);
        lock (_gate)
        {
            if (row.IsError || !LeaseDecision.Holds(row.Value, grant))
            {
                return SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture, $"epoch {grant.Epoch} is not current"));
            }

            var earlier = commit.Writes.Count == 0 ? null : _events.Find(e => e.Event.IdempotencyKey == commit.Writes[0].Event.IdempotencyKey).Event;
            if (earlier is not null)
            {
                return new CommitOutcome(AlreadyRecorded: true, [earlier.Sequence]);
            }

            var stale = commit.Writes.FirstOrDefault(w => (_instances.TryGetValue(w.After.Id, out var now) ? now.Version : 0) != w.ExpectedVersion);
            if (stale is not null)
            {
                return SindriErrors.InstanceChanged.With($"{stale.After.Id.Value} changed; nothing was recorded");
            }

            return Apply(commit, grant);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowInstance?>> Get(InstanceId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<WorkflowInstance?>>(_instances.GetValueOrDefault(id));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> List(InstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<WorkflowInstance>>>(_instances.Values
                .Where(i => query.IncludeEnded || i.Ended is null)
                .OrderByDescending(i => i.UpdatedAt).ThenBy(i => i.Id.Value, StringComparer.Ordinal).Take(Math.Max(query.Max, 1)).ToList());
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Due(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<WorkflowInstance>>>(_instances.Values
                .Where(i => i.Runs && i.Deadline is { } d && d <= now)
                .OrderBy(i => i.Deadline).ThenBy(i => i.Id.Value, StringComparer.Ordinal).Take(Math.Max(max, 1)).ToList());
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Children(InstanceId parent, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<WorkflowInstance>>>(_instances.Values
                .Where(i => i.Parent == parent).OrderBy(i => i.StartedAt).ThenBy(i => i.Id.Value, StringComparer.Ordinal).ToList());
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> History(InstanceId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(_events.Where(e => e.Instance == id).Select(e => e.Event).ToList());
        }
    }

    private Result<CommitOutcome> Apply(WorkflowCommit commit, LeaseGrant grant)
    {
        var sequences = new List<long>();
        foreach (var write in commit.Writes)
        {
            var recorded = new LedgerEvent(_events.Count + 1, grant.Epoch, write.Event.Envelope, write.Event.IdempotencyKey, write.Event.Item);
            _events.Add((write.After.Id, recorded));
            _instances[write.After.Id] = write.After;
            sequences.Add(recorded.Sequence);
        }

        _commands.AddRange(commit.Commands);
        return new CommitOutcome(AlreadyRecorded: false, sequences);
    }
}
