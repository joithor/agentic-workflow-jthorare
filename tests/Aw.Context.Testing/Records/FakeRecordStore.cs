using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>The in-memory <see cref="IRecordStore"/>; it passes <see cref="RecordStoreContract{TStore}"/>.</summary>
public sealed class FakeRecordStore : IRecordStore
{
    /// <inheritdoc />
    public ValueTask<Result<CommitReport>> Commit(RecordBatch batch, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<StoredRecord?>> Get(RecordKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RecordChunk>>> Chunks(RecordKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<StoredRecord>>> List(RecordFilter filter, RecordKey? after, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<SyncPosition>> Position(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<int>> PutVectors(IReadOnlyList<VectorWrite> vectors, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<VectorRow>>> Vectors(VectorSpace space, long afterSeq, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<int>> DeleteVectors(VectorSpace space, IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<StoreStamps>> Stamps(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => throw new NotSupportedException();
}
