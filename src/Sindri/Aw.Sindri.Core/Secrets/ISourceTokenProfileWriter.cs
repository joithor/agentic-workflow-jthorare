using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>Writes the secret pointer of one record source (`context.sources.github.token`) into the live profile, keeping every other key and comment as they were.</summary>
public interface ISourceTokenProfileWriter
{
    /// <summary>Sets `context.sources.&lt;source&gt;.token` to <paramref name="secretPointer"/>. The source's entry has to exist already: its scope (repos, channels) is the owner's to write.</summary>
    /// <param name="source">The source key: `github`.</param>
    /// <param name="secretPointer">The secret pointer, such as `keychain:sindri-index/github`; never the secret itself.</param>
    /// <returns>True when the file changed; false when the pointer was already as given; SND-INDEX-006 when the profile has no such entry; SND-PROFILE-002 when the layout is one Sindri won't edit.</returns>
    public Result<bool> SetToken(string source, string secretPointer);
}
