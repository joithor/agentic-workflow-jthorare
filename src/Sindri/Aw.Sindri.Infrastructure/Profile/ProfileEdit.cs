using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The text edit that puts Sindri's managed Onyx entry into a profile and keeps everything else.</summary>
internal static class ProfileEdit
{
    /// <summary>Sets `context.sources.onyx` in <paramref name="yaml"/>.</summary>
    /// <param name="yaml">The profile text, or null or empty for none.</param>
    /// <param name="user">The `user` of a profile created from nothing.</param>
    /// <param name="port">The loopback port.</param>
    /// <param name="pointer">The token pointer text.</param>
    /// <param name="version">The pinned release tag.</param>
    /// <returns>The new text, or SND-CLI-003.</returns>
    internal static Result<string> SetOnyxSource(string? yaml, string user, int port, string pointer, string version)
    {
        _ = (yaml, user, port, pointer, version);
        throw new NotSupportedException();
    }
}
