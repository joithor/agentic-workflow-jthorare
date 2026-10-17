using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>IProfileWriter over the live profile file: read, edited as text, written back atomically and owner-only.</summary>
/// <param name="settings">Where the profile is.</param>
/// <param name="user">The `user` of a profile created from nothing.</param>
internal sealed class FileProfileWriter(SindriSettings settings, string user) : IProfileWriter
{
    /// <inheritdoc />
    public Result<ProfileWriteOutcome> SetOnyxSource(int port, SecretPointer token, string version)
    {
        _ = (settings, user, port, token, version);
        _ = ProfileEdit.SetOnyxSource(yaml: null, user, port, token.ToString(), version);
        throw new NotSupportedException();
    }
}
