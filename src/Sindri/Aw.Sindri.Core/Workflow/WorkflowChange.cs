using System.Text.Json.Serialization;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The payload of one workflow event: what changed, as a delta <see cref="InstanceFold"/> applies. Replaying an
/// instance's changes in order reproduces it (§17.28.5).
/// </summary>
/// <param name="Instance">The instance.</param>
/// <param name="Kind">What changed.</param>
/// <param name="Version">The instance's version after the change.</param>
/// <param name="At">When.</param>
/// <param name="Start">For 'started': the new instance.</param>
/// <param name="From">For a transition: the state it left.</param>
/// <param name="To">For a transition: the state it entered.</param>
/// <param name="On">For a transition: the signal.</param>
/// <param name="Sha">The head sha the change is bound to, if any.</param>
/// <param name="Gates">For a transition or an escalation: the gates evaluated.</param>
/// <param name="Facts">For a transition: the outputs the signal set.</param>
/// <param name="Loop">For a transition or a park: the loop it counts or that parked it.</param>
/// <param name="Findings">For a loop round: its findings.</param>
/// <param name="Spend">Tokens and turns the signal reported.</param>
/// <param name="Hold">For a park or escalation: the hold.</param>
/// <param name="Deadline">The timer after the change.</param>
/// <param name="Paused">For a takeover: the timer's remaining time; for an unpark while taken over: the state's full timeout.</param>
/// <param name="Evidence">For 'evidence': what was registered.</param>
/// <param name="Extra">For an unpark: budget the human granted.</param>
/// <param name="Completed">For a transition: true when it entered a terminal state.</param>
/// <param name="Effects">The outbox command kinds emitted with the change.</param>
/// <param name="Child">The child instance the change concerns (a started child, or the child that propagated it).</param>
/// <param name="Detail">A human-readable note (a cancel reason, a park detail).</param>
public sealed record WorkflowChange(
    InstanceId Instance,
    ChangeKind Kind,
    long Version,
    DateTimeOffset At,
    WorkflowInstance? Start = null,
    string? From = null,
    string? To = null,
    string? On = null,
    HeadSha? Sha = null,
    IReadOnlyList<GateRecord>? Gates = null,
    IReadOnlyDictionary<string, string>? Facts = null,
    string? Loop = null,
    IReadOnlyList<string>? Findings = null,
    Spend? Spend = null,
    InstanceHold? Hold = null,
    DateTimeOffset? Deadline = null,
    TimeSpan? Paused = null,
    EvidenceRef? Evidence = null,
    Spend? Extra = null,
    bool Completed = false,
    IReadOnlyList<string>? Effects = null,
    InstanceId? Child = null,
    string? Detail = null)
{
    private static readonly Dictionary<ChangeKind, string> _types = new()
    {
        [ChangeKind.Started] = "started",
        [ChangeKind.Transitioned] = "transitioned",
        [ChangeKind.Parked] = "parked",
        [ChangeKind.Unparked] = "unparked",
        [ChangeKind.Escalated] = "escalated",
        [ChangeKind.Cancelled] = "cancelled",
        [ChangeKind.TakenOver] = "taken-over",
        [ChangeKind.HandedBack] = "handed-back",
        [ChangeKind.Retried] = "retried",
        [ChangeKind.Evidence] = "evidence",
        [ChangeKind.DeadlineExtended] = "deadline-extended",
    };

    /// <summary>The ledger event type: 'workflow.' and the kind's word, for example 'workflow.transitioned'.</summary>
    [JsonIgnore]
    public string EventType => "workflow." + _types[Kind];
}
