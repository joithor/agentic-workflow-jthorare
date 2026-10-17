namespace Aw.Sindri.Core.Profile;

/// <summary>What writing Onyx's entry into the live profile did.</summary>
public enum ProfileWriteOutcome
{
    /// <summary>There was no profile file: one was created with the entry.</summary>
    Created,

    /// <summary>The profile existed and the entry was added or replaced.</summary>
    Changed,

    /// <summary>The profile already held exactly this entry; nothing was written.</summary>
    Unchanged,
}
