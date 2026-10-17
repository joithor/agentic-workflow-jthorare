using Aw.Kernel.Testing;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

// The real writer over stand-in tools (libsecret) and a real temp directory, read back by the real reader.
[Lane(TestLane.Integration)]
public sealed class SecretWriterContractTests : SecretWriterContract
{
    protected override SecretWriterRig Create() => new RealRig();

    private sealed class RealRig : SecretWriterRig
    {
        private readonly string _home = Directory.CreateTempSubdirectory("sindri-secret-").FullName;
        private readonly PointerSecrets _reader;

        public RealRig()
        {
            var stub = new StubKeychainRunner(KeychainPlatform.Linux);
            _reader = new PointerSecrets(static _ => null, stub, KeychainPlatform.Linux);
            Writer = new SecretWriter(stub, _reader, _home, KeychainPlatform.Linux);
        }

        public override ISecretWriter Writer { get; }

        public override string? Read(SecretPointer secretPointer) => _reader.Resolve(secretPointer) is { IsSuccess: true } read ? read.Value : null;

        public override void Dispose()
        {
            Directory.Delete(_home, recursive: true);
            base.Dispose();
        }
    }
}
