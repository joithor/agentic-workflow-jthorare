using Aw.Kernel;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>ISecretWriter over the macOS keychain, libsecret and an owner-only file.</summary>
/// <param name="runner">Runs security and secret-tool.</param>
/// <param name="pointers">Reads the entry back.</param>
/// <param name="sindriHome">Where the fallback file goes.</param>
/// <param name="platform">Which store to write; null takes this machine's.</param>
internal sealed class SecretWriter(IToolRunner runner, ISecretPointers pointers, string sindriHome, KeychainPlatform? platform = null) : ISecretWriter
{
    /// <inheritdoc />
    public Result<SecretPointer> Store(string value)
    {
        _ = (runner, pointers, sindriHome, platform, value);
        throw new NotSupportedException();
    }
}
