using Aw.Kernel;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// Writes Sindri's own Onyx into the live profile (spec §4.1 step 9) and nothing else: `context.sources.onyx` with the managed entry. Every other key and comment of the
/// profile is left as it is; an unmanaged or hand-written `onyx` entry is never replaced. Writing a profile changes its hash, so it needs approving again.
/// </summary>
public interface IProfileWriter
{
    /// <summary>Sets `context.sources.onyx` to `url: http://127.0.0.1:&lt;port&gt;`, the token pointer, the version and `managed: true`.</summary>
    /// <param name="port">The loopback port Onyx is on.</param>
    /// <param name="token">The pointer to Sindri's key (never the key).</param>
    /// <param name="version">The pinned release tag.</param>
    /// <returns>What happened; SND-CLI-003 when the profile has a shape that can't be edited safely or a different `onyx` entry (the message holds the snippet to paste); SND-PROFILE-001 or 002 when the file can't be read or parsed.</returns>
    public Result<ProfileWriteOutcome> SetOnyxSource(int port, SecretPointer token, string version);
}
