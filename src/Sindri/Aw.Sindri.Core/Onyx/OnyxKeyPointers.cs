using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Where Sindri's Onyx key lives, and the one name the secret writer and the profile both use for it.</summary>
public static class OnyxKeyPointers
{
    /// <summary>The key's service in the keychain or libsecret.</summary>
    public const string Service = "onyx";

    /// <summary>The key's account in the keychain or libsecret, and the key's name in Onyx.</summary>
    public const string Account = "sindri-onyx";

    /// <summary>Gets the pointer `keychain:onyx/sindri-onyx`.</summary>
    public static SecretPointer Keychain { get; } = SecretPointer.Parse($"keychain:{Service}/{Account}").Value;

    /// <summary>The fallback file's pointer: `file:&lt;home&gt;/secrets/onyx-sindri-onyx`.</summary>
    /// <param name="sindriHome">The Sindri home (an absolute path).</param>
    /// <returns>The pointer.</returns>
    public static SecretPointer FileFor(string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        return SecretPointer.Parse("file:" + FilePath(sindriHome)).Value;
    }

    /// <summary>The fallback file's path.</summary>
    /// <param name="sindriHome">The Sindri home.</param>
    /// <returns>`&lt;home&gt;/secrets/onyx-sindri-onyx`.</returns>
    public static string FilePath(string sindriHome)
    {
        ArgumentNullException.ThrowIfNull(sindriHome);
        return sindriHome.TrimEnd('/') + "/secrets/" + Service + "-" + Account;
    }
}
