using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The universal states every recipe gets (§17.28.1): any → cancelled; active|wait → parked(reason) and back on the
/// human's answer; any non-terminal → taken-over → handback with timers paused; plus registering evidence.
/// </summary>
public static class CommandDecisions
{
    /// <summary>Cancel: any instance that hasn't ended.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="reason">Why, for the record.</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005 when it already ended.</returns>
    public static Result<WorkflowChange> Cancel(WorkflowInstance instance, string reason, DateTimeOffset at) =>
        Ended(instance) is { } ended ? ended : new WorkflowChange(instance.Id, ChangeKind.Cancelled, instance.Version + 1, at, Detail: reason);

    /// <summary>Park with a reason: an instance that hasn't ended and isn't already held.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="hold">The park (kind parked, with its reason and detail).</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005.</returns>
    public static Result<WorkflowChange> Park(WorkflowInstance instance, InstanceHold hold, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return Ended(instance) is { } ended ? ended : (instance.Hold is not null
            ? Refused(instance, "is already held; unpark it first")
            : new WorkflowChange(instance.Id, ChangeKind.Parked, instance.Version + 1, at, Hold: hold, Detail: hold.Detail));
    }

    /// <summary>Unpark: lift a park or escalation; the state's timer restarts and the loop that parked it is reset.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="extra">Budget the human grants on top.</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005 when it isn't held.</returns>
    public static Result<WorkflowChange> Unpark(Recipe recipe, WorkflowInstance instance, Spend extra, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Ended(instance) is { } ended ? ended : (instance.Hold is null
            ? Refused(instance, "isn't parked or escalated")
            : new WorkflowChange(instance.Id, ChangeKind.Unparked, instance.Version + 1, at, Extra: extra,
                Deadline: instance.TakenOver ? null : Deadlines.For(recipe.State(instance.State), at)));
    }

    /// <summary>Take over: the human works the item; timers pause with their remaining time kept.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005.</returns>
    public static Result<WorkflowChange> TakeOver(WorkflowInstance instance, DateTimeOffset at) =>
        Ended(instance) is { } ended ? ended : (instance.TakenOver
            ? Refused(instance, "is already taken over")
            : new WorkflowChange(instance.Id, ChangeKind.TakenOver, instance.Version + 1, at,
                Paused: instance.Deadline is { } d ? (d > at ? d - at : TimeSpan.Zero) : null));

    /// <summary>Hand back: timers resume with the time they had left.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005 when it isn't taken over.</returns>
    public static Result<WorkflowChange> HandBack(WorkflowInstance instance, DateTimeOffset at) =>
        Ended(instance) is { } ended ? ended : (!instance.TakenOver
            ? Refused(instance, "isn't taken over")
            : new WorkflowChange(instance.Id, ChangeKind.HandedBack, instance.Version + 1, at,
                Deadline: instance.Paused is { } left && instance.Hold is null ? at + left : null));

    /// <summary>Register evidence for the instance (§17.11): only registered evidence counts toward a transition.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="evidence">The evidence.</param>
    /// <param name="at">When.</param>
    /// <returns>The change, or SND-WF-005 when it ended.</returns>
    public static Result<WorkflowChange> Register(WorkflowInstance instance, EvidenceRef evidence, DateTimeOffset at) =>
        Ended(instance) is { } ended ? ended : new WorkflowChange(instance.Id, ChangeKind.Evidence, instance.Version + 1, at, Sha: evidence.Sha, Evidence: evidence);

    private static Error? Ended(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return instance.Ended is null ? null : SindriErrors.TransitionNotAllowed.With($"{instance.Id.Value} is {WorkflowWords.Status(instance)}");
    }

    private static Error Refused(WorkflowInstance instance, string why) =>
        SindriErrors.TransitionNotAllowed.With($"{instance.Id.Value} {why} (it is {WorkflowWords.Status(instance)})");
}
