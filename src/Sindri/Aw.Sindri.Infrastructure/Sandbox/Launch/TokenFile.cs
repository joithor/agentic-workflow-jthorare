using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Writes the agent token file of a session home: the folder 0700, the file 0600, a temp file in the same folder renamed over it so a reader sees the old token or the new one.</summary>
internal static class TokenFile
{
    private const UnixFileMode OwnerDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Puts <paramref name="token"/> in the session home's token file, replacing what was there.</summary>
    /// <param name="sessionHome">The launch's session home.</param>
    /// <param name="token">The token.</param>
    /// <param name="create">False for a renewal: a folder that is gone means the launch closed, and the token is not written back.</param>
    /// <returns>False when <paramref name="create"/> is false and the folder is gone.</returns>
    internal static bool Replace(string sessionHome, string token, bool create = true)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("the sandbox runs on Linux");
        }

        var directory = SessionHomePaths.GhTokenDirectory(sessionHome);
        if (!create && !Directory.Exists(directory))
        {
            return false;
        }

        _ = Directory.CreateDirectory(directory, OwnerDirectory);
        File.SetUnixFileMode(directory, OwnerDirectory);
        var temp = $"{directory}/.token.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(temp, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = OwnerFile }))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(token + "\n");
            }

            File.Move(temp, SessionHomePaths.GhTokenFile(sessionHome), overwrite: true);
            return true;
        }
        finally
        {
            File.Delete(temp);   // gone after the move; left only when the write or the move failed
        }
    }
}
