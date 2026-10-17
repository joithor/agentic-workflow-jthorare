using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class FakeOwnerConsoleContractTests : OwnerConsoleContract
{
    protected override OwnerConsoleRig Create() => new FakeRig();

    private sealed class FakeRig : OwnerConsoleRig
    {
        private readonly FakeOwnerConsole _fake = new();

        public override IOwnerConsole Console => _fake;

        public override string Terminal => _fake.Terminal;

        public override void Feed(params string?[] answers) => _fake.Feed(answers);
    }
}
