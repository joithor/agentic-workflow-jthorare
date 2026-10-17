namespace Aw.Sindri.Core.Onyx;

/// <summary>The one-line fixes the account-setup errors (SND-ONYX-007 to 009) end with.</summary>
public static class OnyxFixes
{
    /// <summary>Gets the fix for SND-ONYX-007.</summary>
    public const string LoginNotAdmin = "make that Onyx account an admin in Onyx's Users page, or log in as the first user";

    /// <summary>Gets the fix for SND-ONYX-008.</summary>
    public const string KeyFailed = "run `sindri onyx up` again; if it repeats, check Onyx at the printed url";

    /// <summary>Gets the fix for SND-ONYX-009.</summary>
    public const string SecretWriteFailed = "unlock the keychain or install libsecret-tools, or free the file fallback's directory, then run `sindri onyx up` again";
}
