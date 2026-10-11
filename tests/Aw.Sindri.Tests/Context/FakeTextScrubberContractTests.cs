using Aw.Kernel.Testing;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Unit)]
public sealed class FakeTextScrubberContractTests : TextScrubberContract
{
    protected override ITextScrubber Create() => new FakeTextScrubber();
}
