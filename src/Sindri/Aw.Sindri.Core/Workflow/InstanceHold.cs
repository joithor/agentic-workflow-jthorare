using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A park or escalation: its reason (parks only), what happened, and the loop that parked it (reset on unpark).</summary>
/// <param name="Kind">Parked or escalated.</param>
/// <param name="Reason">Why it is parked; null for an escalation.</param>
/// <param name="Detail">What happened, in one line.</param>
/// <param name="Loop">The loop whose cap or no-progress rule parked it, if any.</param>
/// <param name="Child">The child whose park or escalation propagated here, if any (its unpark lifts this hold).</param>
public sealed record InstanceHold(HoldKind Kind, ParkReason? Reason, string Detail, string? Loop = null, InstanceId? Child = null);
