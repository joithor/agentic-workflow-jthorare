namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Decides the fix of one denial by its kind and rule (sandbox spec §3.6, R3-07); the API puts it on every entry so the app never decides it.</summary>
public static class DenialFixes
{
    /// <summary>The entry with its fix, and with a reason when the fix is 'never'.</summary>
    /// <param name="entry">A counted denial.</param>
    /// <returns>The same entry with <see cref="DenialEntry.Fix"/> and <see cref="DenialEntry.Reason"/> set.</returns>
    public static DenialEntry Decide(DenialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        throw new NotSupportedException();
    }
}
