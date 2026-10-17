using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// IProfileWriter over the live profile file: its text is edited by <see cref="ProfileEdit"/>, then written through a 0600 temporary file and a rename (so a reader never sees half a
/// profile). A profile that already holds the entry isn't written at all. A profile that can't be read is SND-PROFILE-001, as <see cref="YamlProfileSource"/> says it.
/// </summary>
/// <param name="settings">Where the profile is.</param>
/// <param name="user">The `user` of a profile created from nothing.</param>
internal sealed class FileProfileWriter(SindriSettings settings, string user) : IProfileWriter
{
    /// <inheritdoc />
    public Result<ProfileWriteOutcome> SetOnyxSource(int port, SecretPointer token, string version)
    {
        ArgumentNullException.ThrowIfNull(token);
        var path = settings.ProfilePath;
        try
        {
            var existed = File.Exists(path);
            var current = existed ? File.ReadAllText(path) : null;
            var edited = ProfileEdit.SetOnyxSource(current, user, port, token.ToString(), version);
            if (edited.IsError)
            {
                return edited.Error;
            }

            if (existed && string.Equals(current, edited.Value, StringComparison.Ordinal))
            {
                return ProfileWriteOutcome.Unchanged;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a profile path always has a directory
            OwnerOnly.WriteAtomically(path, Encoding.UTF8.GetBytes(edited.Value));
            return existed ? ProfileWriteOutcome.Changed : ProfileWriteOutcome.Created;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read or written: {exception.Message}");
        }
    }
}
