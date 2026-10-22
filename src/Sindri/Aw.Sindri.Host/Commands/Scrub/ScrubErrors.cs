using Aw.Kernel;

namespace Aw.Sindri.Host.Commands.Scrub;

/// <summary>The scrub verb's error codes (docs/sindri/errors.md); local to the verb, which never reaches the control plane.</summary>
internal static class ScrubErrors
{
    /// <summary>SND-SCRUB-002: staged changes hold likely secrets (an attention result: exit 1).</summary>
    internal static readonly ErrorDefinition StagedSecrets = new("SND-SCRUB-002", ErrorKind.Conflict, "Staged changes contain likely secrets");

    /// <summary>SND-SCRUB-003: the pre-commit hook is someone else's, or core.hooksPath points it at a directory sindri didn't create.</summary>
    internal static readonly ErrorDefinition ForeignHook = new("SND-SCRUB-003", ErrorKind.Conflict, "The pre-commit hook is not sindri's");

    /// <summary>SND-SCRUB-004: the verb ran outside a git repo.</summary>
    internal static readonly ErrorDefinition NotARepo = new("SND-SCRUB-004", ErrorKind.Invalid, "Not inside a git repo");

    /// <summary>SND-SCRUB-005: git failed, so the staged changes were not scanned.</summary>
    internal static readonly ErrorDefinition GitFailed = new("SND-SCRUB-005", ErrorKind.Unavailable, "git could not list the staged changes");

    /// <summary>SND-SCRUB-007: a scan timed out, so the text was withheld.</summary>
    internal static readonly ErrorDefinition Blocked = new("SND-SCRUB-007", ErrorKind.Timeout, "The scan timed out, so the text was withheld");
}
