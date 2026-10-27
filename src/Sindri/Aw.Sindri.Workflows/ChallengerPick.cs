using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Which launcher agent runs a contrast Step (#1008): the second opinion runs on an agent other than the one that ran the Step it checks (the drafter), picked from the
/// catalog entries an unattended Step may use, and carries the drafter's launch to the model pick. Stub (red): the real pick follows.
/// </summary>
/// <param name="catalog">The agent catalog.</param>
/// <param name="launches">The launches view, for the drafter's agent and model.</param>
/// <param name="approved">The approved profile, for the argv hashes approved for Steps.</param>
internal sealed class ChallengerPick(IAgentCatalog catalog, ILaunchViews launches, IApprovedProfile approved)
{
    internal IAgentCatalog Catalog { get; } = catalog;

    internal ILaunchViews Launches { get; } = launches;

    internal IApprovedProfile Approved { get; } = approved;

    /// <summary>The agent the contrast Step runs on, and the drafter it differs from.</summary>
    /// <param name="ask">The Step's launch and the Step it differs from.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The choice.</returns>
    public ValueTask<Result<ChallengerChoice>> Pick(ChallengerAsk ask, CancellationToken cancellationToken) => throw new NotSupportedException();
}
