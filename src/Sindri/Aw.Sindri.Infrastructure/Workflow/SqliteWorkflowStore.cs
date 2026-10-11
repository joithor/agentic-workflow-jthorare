using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Kernel.Outbox;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Workflow instances in the ledger file. A commit is one BEGIN IMMEDIATE: the lease epoch (fencing), the first event's
/// key (a replay writes nothing), each instance's version, then the events (with their instance), the rows and the
/// outbox commands (Kernel's SqliteOutbox on the same session), committed together.
/// </summary>
internal sealed class SqliteWorkflowStore(LedgerDatabase database, JsonSerializerOptions options, TimeProvider clock) : IWorkflowStore
{
    private readonly InstanceMapper _instances = new(options);

    public async ValueTask<Result<CommitOutcome>> Commit(WorkflowCommit commit, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commit);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        var begun = writable.IsError ? null : await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun is null || begun.IsError)
        {
            return begun is null ? writable.Error : LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            return await Inside(begun.Value, commit, grant, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<CommitOutcome>> Inside(SqliteUnitOfWork session, WorkflowCommit commit, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var lease = await LeaseReads.Current(session, cancellationToken).ConfigureAwait(false);
        if (lease.IsError || !LeaseDecision.Holds(lease.Value, grant))
        {
            return lease.IsError ? lease.Error : SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture,
                $"epoch {grant.Epoch} of {grant.Holder} is not current; nothing was recorded"));
        }

        var earlier = commit.Writes.Count == 0 ? null : await Earlier(session, commit.Writes[0].Event.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (earlier is { } replay)
        {
            return replay.IsError ? replay.Error : new CommitOutcome(AlreadyRecorded: true, [replay.Value]);
        }

        return await Write(session, commit, grant, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<WorkflowInstance?>> Get(InstanceId id, CancellationToken cancellationToken)
    {
        var rows = await Read(new SqlStatement(WorkflowSql.SelectInstance, c => c.Bind("$id", id.Value)), _instances, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> List(InstanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Read(new SqlStatement(WorkflowSql.SelectList, c => c.Bind("$all", query.IncludeEnded ? 1 : 0).Bind("$max", Math.Max(query.Max, 1))), _instances, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Due(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        Read(new SqlStatement(WorkflowSql.SelectDue, c => c.Bind("$now", now).Bind("$max", Math.Max(max, 1))), _instances, cancellationToken);

    public ValueTask<Result<IReadOnlyList<WorkflowInstance>>> Children(InstanceId parent, CancellationToken cancellationToken) =>
        Read(new SqlStatement(WorkflowSql.SelectChildren, c => c.Bind("$id", parent.Value)), _instances, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> History(InstanceId id, CancellationToken cancellationToken) =>
        Read(new SqlStatement(WorkflowSql.SelectHistory, c => c.Bind("$id", id.Value)), LedgerEventMapper.Instance, cancellationToken);

    private static async ValueTask<Result<long>?> Earlier(SqliteUnitOfWork session, string key, CancellationToken ct)
    {
        var rows = await SqliteQueries.List(session, new SqlStatement(WorkflowSql.SelectByKey, c => c.Bind("$key", key)), (IRowMapper<long>)CountMapper.Instance, ct)
            .ConfigureAwait(false);
        return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? null : rows.Value[0];
    }

    private async ValueTask<Result<CommitOutcome>> Write(SqliteUnitOfWork session, WorkflowCommit commit, LeaseGrant grant, CancellationToken ct)
    {
        var sequences = new List<long>();
        foreach (var write in commit.Writes)
        {
            var written = await WriteOne(session, write, grant, ct).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }

            sequences.Add(written.Value);
        }

        var outbox = new SqliteOutbox(session, clock, OutboxRetryPolicy.Default);
        foreach (var command in commit.Commands)
        {
            var queued = await outbox.Enqueue(command, ct).ConfigureAwait(false);
            if (queued.IsError)
            {
                return LedgerDatabase.Translate(queued.Error);
            }
        }

        var committed = await session.Commit(ct).ConfigureAwait(false);
        return committed.IsError ? LedgerDatabase.Translate(committed.Error) : new CommitOutcome(AlreadyRecorded: false, sequences);
    }

    private async ValueTask<Result<long>> WriteOne(SqliteUnitOfWork session, InstanceWrite write, LeaseGrant grant, CancellationToken ct)
    {
        var stored = await SqliteQueries.List(session, new SqlStatement(WorkflowSql.SelectVersion, c => c.Bind("$id", write.After.Id.Value)),
            (IRowMapper<long>)CountMapper.Instance, ct).ConfigureAwait(false);
        if (stored.IsError)
        {
            return LedgerDatabase.Translate(stored.Error);
        }

        var version = stored.Value.Count == 0 ? 0 : stored.Value[0];
        if (version != write.ExpectedVersion)
        {
            return SindriErrors.InstanceChanged.With(string.Create(CultureInfo.InvariantCulture,
                $"{write.After.Id.Value} is at version {version}, not {write.ExpectedVersion}; nothing was recorded"));
        }

        var sequence = await SqliteQueries.One(session, WorkflowRows.Event(write, grant), (IRowMapper<long>)CountMapper.Instance, ct).ConfigureAwait(false);
        if (sequence.IsError)
        {
            return LedgerDatabase.Translate(sequence.Error);
        }

        var row = await SqliteQueries.Execute(session, WorkflowRows.Instance(write.After, Snapshot(write.After)), ct).ConfigureAwait(false);
        return row.IsError ? LedgerDatabase.Translate(row.Error) : sequence.Value;
    }

    private string Snapshot(WorkflowInstance instance) => Encoding.UTF8.GetString(AwJson.Serialize(instance, options));

    private async ValueTask<Result<IReadOnlyList<T>>> Read<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken ct)
    {
        var schema = await database.Schema(ct).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied < LedgerDatabase.Migrations.Count)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await database.Sessions.BeginRead(ct).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, mapper, ct).ConfigureAwait(false);
            return rows.IsError ? LedgerDatabase.Translate(rows.Error) : rows;
        }
    }
}
