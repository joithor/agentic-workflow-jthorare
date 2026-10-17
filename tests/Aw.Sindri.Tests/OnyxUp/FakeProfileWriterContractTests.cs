using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

[Lane(TestLane.Unit)]
public sealed class FakeProfileWriterContractTests : ProfileWriterContract
{
    protected override ProfileWriterRig Create(string? text) => new FakeRig(text);

    private sealed class FakeRig(string? text) : ProfileWriterRig
    {
        private readonly FakeProfileWriter _fake = new() { Text = text };

        public override IProfileWriter Writer => _fake;

        public override string? Text
        {
            get => _fake.Text;
            set => _fake.Text = value;
        }
    }
}
