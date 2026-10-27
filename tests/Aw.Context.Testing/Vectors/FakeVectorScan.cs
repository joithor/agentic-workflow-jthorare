using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IVectorScan"/> that scores every row with a plain dot product; it passes <see cref="VectorScanContract{TScan}"/>.</summary>
public sealed class FakeVectorScan : IVectorScan
{
    /// <inheritdoc />
    public Result<IReadOnlyList<VectorHit>> Top(VectorMatrix matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count) => throw new NotSupportedException();
}
