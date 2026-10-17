using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

[Lane(TestLane.Unit)]
public sealed class FakeSecretWriterContractTests : SecretWriterContract
{
    protected override SecretWriterRig Create() => new FakeRig();

    private sealed class FakeRig : SecretWriterRig
    {
        private readonly FakeSecretWriter _fake = new();

        public override ISecretWriter Writer => _fake;

        public override string? Read(SecretPointer secretPointer) => secretPointer.Equals(OnyxKeyPointers.Keychain) ? _fake.Stored.LastOrDefault() : null;
    }
}
