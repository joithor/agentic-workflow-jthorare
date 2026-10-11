using Aw.Kernel.Events;

namespace Aw.Sindri.Core.Workflow;

/// <summary>A change recorded into a draft: the instance after it, the change, and its event (which propagated changes are caused by).</summary>
/// <param name="After">The instance after the change.</param>
/// <param name="Change">The change.</param>
/// <param name="Event">Its event envelope.</param>
internal sealed record AppliedChange(WorkflowInstance After, WorkflowChange Change, EventEnvelope<string> Event);
