using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// Stores Sindri's Onyx key (spec §5.3) and nothing else: the entry's service and account are fixed (`onyx` and `sindri-onyx`), so no parameter can name another
/// secret and a caller can't overwrite the user's other credentials. The value never reaches a process's argument list, a log or an error message.
/// </summary>
public interface ISecretWriter
{
    /// <summary>Writes the key where <see cref="ISecretPointers"/> reads it back: the keychain (macOS) or libsecret (Linux), else an owner-only file.</summary>
    /// <param name="value">The key.</param>
    /// <returns>The pointer the profile holds (`keychain:onyx/sindri-onyx`, or the `file:` fallback); SND-ONYX-009 when no store took it.</returns>
    public Result<SecretPointer> Store(string value);
}
