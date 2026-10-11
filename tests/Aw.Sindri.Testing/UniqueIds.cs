using Aw.Kernel.Ids;

namespace Aw.Sindri.Testing;

/// <summary>Ids that never repeat across instances within a test run (for outbox commands built in separate places).</summary>
public sealed class UniqueIds : IIdGenerator
{
    private static long _next = 1_000_000;

    /// <inheritdoc/>
    public Guid NewId()
    {
        var n = Interlocked.Increment(ref _next);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, n);
        bytes[15] = 0x5a;
        return new Guid(bytes);
    }
}
