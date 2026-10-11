using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Infrastructure.Context;
using Aw.Sindri.Testing.Context;

namespace Aw.Sindri.Tests.Context;

[Lane(TestLane.Unit)]
public sealed class RedactionScrubberContractTests : TextScrubberContract
{
    protected override ITextScrubber Create() => new RedactionScrubber(Redactor.Default);
}
