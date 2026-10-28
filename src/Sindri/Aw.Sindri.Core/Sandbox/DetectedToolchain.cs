using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>One toolchain the detector found, and the entries it suggests for it.</summary>
/// <param name="Name">The toolchain's name for the owner: Node, Python, Go or dotnet.</param>
/// <param name="Evidence">The first project file that showed it.</param>
/// <param name="Entries">The entries this toolchain adds.</param>
public sealed record DetectedToolchain(string Name, string Evidence, SandboxRepoSettings Entries)
{
    /// <summary>The owner-facing line: the toolchain, the file that showed it and what is offered.</summary>
    /// <returns>One line, no line break.</returns>
    public string Line() => throw new NotSupportedException();
}
