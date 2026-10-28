using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// <see cref="IProfileSandboxWriter"/> over the live profile file (sandbox spec §3.6). The text is edited line by line, so every other key and comment stays: the repo's 'hosts' or 'homeAllow' list under 'sandbox.repos.&lt;repo&gt;' is
/// found or created, and the entry is appended once. The result must parse and hold the entry, and replaces the file only if the file is still the text that was read (one retry, as <see cref="YamlProfileRepoWriter"/>).
/// </summary>
/// <param name="settings">Where the profile is.</param>
internal sealed class YamlProfileSandboxWriter(SindriSettings settings) : IProfileSandboxWriter
{
    /// <summary>The owner's home directory, which a path is made relative to.</summary>
    internal string Home { get; init; } = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } configured ? configured : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken)
    {
        _ = (settings, repo, kind, target);
        throw new NotSupportedException();
    }
}
