using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The one check of a profile's `context:` section that `profile validate` and `profile approve` share: the context engine's rules (the keys, the patterns, the repos), then what only
/// Sindri knows (a managed Onyx entry must pin the release this build runs). An invalid section is never approved.
/// </summary>
public interface IProfileContextCheck
{
    /// <summary>Checks the profile's context section against its repos.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Null when it holds; otherwise the first error (SND-CONTEXT-001, 002 or 055, naming the key path).</returns>
    public Error? Check(SindriProfile profile);
}
