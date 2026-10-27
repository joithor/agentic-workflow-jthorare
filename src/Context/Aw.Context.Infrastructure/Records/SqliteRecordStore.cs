using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The record store in context.db (migration 0018_s4_record_store, spec §4): reads, vectors, the position and the purge are here, the page commit is in the other part of this class. Every write
/// is one 'BEGIN IMMEDIATE' transaction on its own connection with 'secure_delete' on.
/// </summary>
internal sealed partial class SqliteRecordStore(ContextDatabase database) : IRecordStore
{
    private const float NormTolerance = 1e-4f;

    public async ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : (StoredRecord?)null;
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.Get, c => c.Bind("@key", key.Value)), RecordMappers.Record, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return ContextDatabase.Translate(rows.Error);
            }

            return rows.Value.Count == 0 ? (StoredRecord?)null : await WithLinks(session, rows.Value[0], cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<RecordChunk>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ChunksOf, c => c.Bind("@key", key.Value)), RecordMappers.Chunk, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<StoredRecord>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.List, c => c
                .Bind("@source", filter.Source?.Value)
                .Bind("@repo", filter.Repo?.Value)
                .Bind("@kind", filter.Kind is { } kind ? RecordWire.Of(kind) : null)
                .Bind("@removed", filter.IncludeRemoved)
                .Bind("@after", after?.Value)
                .Bind("@limit", limit));
            var rows = await SqliteQueries.List(session, statement, RecordMappers.Record, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return ContextDatabase.Translate(rows.Error);
            }

            var page = new List<StoredRecord>(rows.Value.Count);
            foreach (var row in rows.Value)
            {
                var full = await WithLinks(session, row, cancellationToken).ConfigureAwait(false);
                if (full.IsError)
                {
                    return full.Error;
                }

                page.Add(full.Value);
            }

            return page;
        }
    }

    public async ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : SyncPosition.Start;
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadPosition, c => c.Bind("@source", source.Value)), RecordMappers.Position, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? SyncPosition.Start : rows.Value[0];
        }
    }

    public async ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var stored = 0;
            foreach (var vector in vectors.Where(v => v.Vec.Length > 0))
            {
                var done = await StoreVector(begun.Value, vector, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }

                stored += done.Value;
            }

            // One stamp per committed embed batch, so the caches reload only what is new (spec §4.4).
            var stamped = stored == 0 ? Results.Success(0) : await Run(begun.Value, RecordSql.BumpRecordStamp, null, cancellationToken).ConfigureAwait(false);
            var committed = stamped.IsError ? stamped.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : stored;
        }
    }

    public async ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 10_000);
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<VectorRow>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var statement = new SqlStatement(RecordSql.ReadVectors, c => c.Bind("@space", RecordWire.Of(space)).Bind("@after", afterSeq).Bind("@limit", limit));
            var rows = await SqliteQueries.List(session, statement, RecordMappers.Vector(space), cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var removed = 0;
            foreach (var key in keys)
            {
                var done = await Run(begun.Value, RecordSql.DeleteVector, c => c.Bind("@space", RecordWire.Of(space)).Bind("@key", key), cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }

                removed += done.Value;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : removed;
        }
    }

    public async ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken)
    {
        var read = await BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : new StoreStamps(0, 0);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.ReadStamps), RecordMappers.Stamps, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? new StoreStamps(0, 0) : rows.Value[0];
        }
    }

    public async ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken)
    {
        var begun = await BeginWrite(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            Action<Microsoft.Data.Sqlite.SqliteCommand> bind = c => c.Bind("@source", source.Value);
            foreach (var sql in RecordSql.Purge)
            {
                var step = await Run(begun.Value, sql, bind, cancellationToken).ConfigureAwait(false);
                if (step.IsError)
                {
                    return step.Error;
                }
            }

            var records = await Run(begun.Value, RecordSql.DeleteRecords, bind, cancellationToken).ConfigureAwait(false);
            var stamped = records.IsError ? records : await Run(begun.Value, RecordSql.BumpRecordStamp, null, cancellationToken).ConfigureAwait(false);
            if (stamped.IsError)
            {
                return stamped.Error;
            }

            return await Finish(begun.Value, deleted: true, records.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<int>> StoreVector(SqliteUnitOfWork session, VectorWrite vector, CancellationToken cancellationToken)
    {
        var chunk = vector.Space == VectorSpace.Chunk;
        void Bind(Microsoft.Data.Sqlite.SqliteCommand c) => c.Bind("@key", vector.Key).Bind("@model", vector.Model).Bind("@hash", vector.TextHash).Bind("@dim", vector.Vec.Length).Bind("@vec", RecordMappers.Bytes(Normalised(vector.Vec.Span)));
        var replaced = await Run(session, chunk ? RecordSql.ReplaceChunkVector : RecordSql.ReplaceNodeVector, Bind, cancellationToken).ConfigureAwait(false);
        return replaced.IsError ? replaced : await Run(session, chunk ? RecordSql.InsertChunkVector : RecordSql.InsertNodeVector, Bind, cancellationToken).ConfigureAwait(false);
    }

    // Cosine is a dot product only when every stored vector has length 1 (spec §4.4); a vector already within tolerance is stored as it came, bit for bit.
    private static float[] Normalised(ReadOnlySpan<float> vec)
    {
        var sum = 0f;
        foreach (var component in vec)
        {
            sum += component * component;
        }

        var norm = MathF.Sqrt(sum);
        var copy = vec.ToArray();
        if (norm > 0 && MathF.Abs(norm - 1f) > NormTolerance)
        {
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] /= norm;
            }
        }

        return copy;
    }

    private async ValueTask<Result<StoredRecord>> WithLinks(SqliteReadSession session, StoredRecord record, CancellationToken cancellationToken)
    {
        var links = await SqliteQueries.List(session, new SqlStatement(RecordSql.LinksOf, c => c.Bind("@key", record.Key.Value)), RecordMappers.Link, cancellationToken).ConfigureAwait(false);
        return links.IsError ? ContextDatabase.Translate(links.Error) : record with { Links = links.Value };
    }

    // A reader on the file, or null when nothing was ever written (a store never created has no records, chunks or vectors).
    private async ValueTask<Result<SqliteReadSession?>> BeginRead(CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : (SqliteReadSession?)null;
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        return read.IsError ? ContextDatabase.Translate(read.Error) : read.Value;
    }

    // A write transaction (BEGIN IMMEDIATE) with secure_delete on, so every page a delete frees is zeroed, not just unlinked.
    private async ValueTask<Result<SqliteUnitOfWork>> BeginWrite(CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        var pragma = await Run(begun.Value, RecordSql.SecureDelete, null, cancellationToken).ConfigureAwait(false);
        if (pragma.IsError)
        {
            await begun.Value.DisposeAsync().ConfigureAwait(false);
            return pragma.Error;
        }

        return begun.Value;
    }

    // Commits, then, when chunks went, compacts the full-text segments and truncates the WAL, so no old page image or segment keeps the deleted words (spec §4.5).
    private async ValueTask<Result<int>> Finish(SqliteUnitOfWork session, bool deleted, int result, CancellationToken cancellationToken)
    {
        var optimized = deleted ? await Run(session, RecordSql.OptimizeFts, null, cancellationToken).ConfigureAwait(false) : Results.Success(0);
        var committed = optimized.IsError ? optimized.Error : await session.Commit(cancellationToken).ConfigureAwait(false);
        database.RestrictFiles();
        if (committed.IsError)
        {
            return ContextDatabase.Translate(committed.Error);
        }

        if (deleted)
        {
            database.TruncateWal();
        }

        return result;
    }

    // A statement's failure is the store being unavailable (SND-CONTEXT-004), whatever the Kernel.Data code was.
    private static async ValueTask<Result<int>> Run(ISqliteSession session, string sql, Action<Microsoft.Data.Sqlite.SqliteCommand>? bind, CancellationToken cancellationToken)
    {
        var done = await SqliteQueries.Execute(session, new SqlStatement(sql, bind), cancellationToken).ConfigureAwait(false);
        return done.IsError ? ContextDatabase.Translate(done.Error) : done;
    }
}
