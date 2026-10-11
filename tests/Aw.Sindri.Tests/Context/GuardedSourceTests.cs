using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Infrastructure.Context;

namespace Aw.Sindri.Tests.Context;

public sealed class GuardedSourceTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task An_exception_from_a_source_becomes_a_failed_source_naming_it()
    {
        var guarded = new GuardedSource(new ThrowingSource(new InvalidOperationException("git didn't start")));

        var answer = await guarded.Search(new SourceQuery(["lease"], 5), TestContext.Current.CancellationToken);

        Assert.Equal(SourceKind.Code, guarded.Kind);
        Assert.Equal(SindriErrors.ContextSourceFailed.Code, answer.Error.Code);
        Assert.Equal("the code source failed unexpectedly (InvalidOperationException: git didn't start)", answer.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancellation_thrown_by_a_source_becomes_a_failed_source()
    {
        var answer = await new GuardedSource(new ThrowingSource(new OperationCanceledException())).Search(new SourceQuery(["lease"], 5), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ContextSourceFailed.Code, answer.Error.Code);
    }

    private sealed class ThrowingSource(Exception toThrow) : IContextSource
    {
        public SourceKind Kind => SourceKind.Code;

        public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => throw toThrow;
    }
}
