using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// A refused agent launch and a session found ended when a suspension ended (sandbox spec §6, §7, SB16; engine P8 U9). Stubs: the decisions come with the green commit.
/// </summary>
public static partial class TransitionDecision
{
    private static Decision NoRuntimeForLaunch(Deciding d, WorkflowInstance instance, RuntimeUnavailable refusal)
    {
        _ = (d, instance, refusal);
        throw new NotSupportedException("a refused launch's no-runtime decision is not built yet");
    }

    private static Decision LaunchParked(Deciding d, WorkflowInstance instance, LaunchRefused refusal)
    {
        _ = (d, instance, refusal);
        throw new NotSupportedException("a refused launch's park is not built yet");
    }

    private static Decision SessionGone(Deciding d, WorkflowInstance instance, SessionLost lost)
    {
        _ = (d, instance, lost);
        throw new NotSupportedException("a session found ended after a suspension is not decided yet");
    }
}
