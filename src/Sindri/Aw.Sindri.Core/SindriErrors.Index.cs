using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-INDEX-001: a source's token pointer does not resolve, the source refused the token, or a file: pointer is wider than 0600.</summary>
    public static readonly ErrorDefinition IndexTokenUnresolved = new("SND-INDEX-001", ErrorKind.Invalid, "A source token pointer does not resolve, the token was refused, or its file is wider than 0600");

    /// <summary>SND-INDEX-002: a repo listed in the profile is unreadable with the token; the message names each one.</summary>
    public static readonly ErrorDefinition IndexRepoUnreadable = new("SND-INDEX-002", ErrorKind.Unavailable, "A listed repo is unreadable with this token");

    /// <summary>SND-INDEX-006: a repo or host is not in the profile; no request was sent.</summary>
    public static readonly ErrorDefinition IndexNotInProfile = new("SND-INDEX-006", ErrorKind.Invalid, "The source, repo or host is not in the profile; no request was sent");
}
