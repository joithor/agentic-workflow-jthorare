using Aw.Kernel;
using Aw.Sindri.Core.Blocks;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// What a failed gate means (§17.28.1, invariant 4). The ladder stops at the first failure, so a top-rung failure means
/// every cheaper gate passed: it escalates. A failure on a lower rung refuses the transition and records nothing.
/// </summary>
public static class GateOutcome
{
    /// <summary>Null when every gate passed; otherwise the escalation, or SND-WF-006 naming the failed gates.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="gates">The gate records of the last candidate tried.</param>
    /// <param name="at">When.</param>
    /// <returns>Null, an escalation change, or the refusal.</returns>
    public static Result<WorkflowChange>? Judge(WorkflowInstance instance, IReadOnlyList<GateRecord> gates, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(gates);
        var failed = gates.Where(g => !g.Passed).ToList();
        if (failed.Count == 0)
        {
            return null;
        }

        var detail = string.Join("; ", failed.Select(g => $"{g.Gate.Value}: {g.Detail}"));
        if (failed.Exists(g => g.Rung is GateRung.Model))
        {
            return new WorkflowChange(instance.Id, ChangeKind.Escalated, instance.Version + 1, at, Gates: gates,
                Hold: new InstanceHold(HoldKind.Escalated, Reason: null, $"a top-rung gate failed: {detail}"), Detail: detail);
        }

        return SindriErrors.GateRefused.With(detail);
    }
}
