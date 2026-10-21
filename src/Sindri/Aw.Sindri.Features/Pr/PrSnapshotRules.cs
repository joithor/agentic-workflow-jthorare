using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// The snapshot's outcome and what it records (PR babysitter spec §4.3, the lite slice). The first that holds wins: merged, closed, a conflict
/// (still 'attention': the lite recipe has no 'conflict' outcome), a required check failed on the head ('ciRed': a fix on a red head can't be checked,
/// so it comes before an open review comment), the rest of 'attention' (changes requested, unresolved threads), 'pending', then 'green'. Checks count
/// only when they ran on the head: checks on an older commit are 'pending', never 'green' or 'ciRed', which closes the window right after our own push,
/// when the forge still shows the old head's checks.
/// </summary>
internal static class PrSnapshotRules
{
    /// <summary>The outcome <paramref name="pull"/> has.</summary>
    /// <param name="pull">The pull request as the forge reports it.</param>
    /// <returns>One of <see cref="PrSnapshotOutcomes"/>.</returns>
    public static string Outcome(PullView pull) => throw new NotSupportedException();

    /// <summary>Whether the checks ran on the pull request's head commit.</summary>
    /// <param name="pull">The pull request.</param>
    /// <returns>True when the commit the checks ran on is the head.</returns>
    public static bool ChecksOnHead(PullView pull) => throw new NotSupportedException();
}
