using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Where an agent Step runs and, for a challenger, who ran the Step it checks (#1008): the profile's target, with the agent swapped for the one <see cref="ChallengerPick"/> chooses
/// when the Step must differ from an earlier Step of its instance.
/// </summary>
/// <param name="targets">The profile's target.</param>
/// <param name="challengers">Picks the challenger's agent and carries the drafter's launch.</param>
internal sealed class AgentPlacing(AgentTargetResolver targets, ChallengerPick challengers)
{
    /// <summary>The place <paramref name="run"/> runs.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The target and the drafter's launch (null for a Step that differs from none); SND-LAUNCH-028 when no agent may run a challenger.</returns>
    public async ValueTask<Result<PlacedStep>> Place(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var target = await targets.Resolve(run.Item, run.Run.Address, cancellationToken).ConfigureAwait(false);
        if (target.IsError || ChallengerSteps.DrafterState(run.Run.Step.Block) is not { } drafterState)
        {
            return target.IsError ? target.Error : new PlacedStep(target.Value, Drafter: null);
        }

        var second = await challengers.Pick(new ChallengerAsk(target.Value.Item, run.Run.Address, AgentLaunches.LaunchIdFor(run.Run), drafterState, target.Value.Agent), cancellationToken).ConfigureAwait(false);
        return second.IsError ? second.Error : new PlacedStep(target.Value with { Agent = second.Value.Agent }, second.Value.Drafter);
    }
}
