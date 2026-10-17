using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Secrets;

// What the writer stores is what the reader (PointerSecrets, which doctor and the query adapter use) resolves, for every form the writer can return.
public sealed class SecretWriterAgreementTests : IDisposable
{
    private const string Key = "on_Zk3-9fQ_aBcDeFg1234567890xyz";

    private readonly string _home = Directory.CreateTempSubdirectory("sindri-secret-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Theory, Trait("Lane", "integration")]
    [InlineData(true)]
    [InlineData(false)]
    public void The_keychain_form_resolves_to_what_was_stored(bool macOS)
    {
        var platform = macOS ? KeychainPlatform.MacOS : KeychainPlatform.Linux;
        var stub = new StubKeychainRunner(platform);
        var reader = new PointerSecrets(static _ => null, stub, platform);

        var pointer = new SecretWriter(stub, reader, _home, platform).Store(Key).Value;

        Assert.Equal(OnyxKeyPointers.Keychain, pointer);
        Assert.Equal(Key, reader.Resolve(pointer).Value);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_file_fallback_resolves_to_what_was_stored()
    {
        var stub = new StubKeychainRunner(KeychainPlatform.Linux) { Failing = true };
        var reader = new PointerSecrets(static _ => null, stub, KeychainPlatform.Linux);

        var pointer = new SecretWriter(stub, reader, _home, KeychainPlatform.Linux).Store(Key).Value;

        Assert.Equal(OnyxKeyPointers.FileFor(_home), pointer);
        Assert.Equal(Key, reader.Resolve(pointer).Value);
    }
}
