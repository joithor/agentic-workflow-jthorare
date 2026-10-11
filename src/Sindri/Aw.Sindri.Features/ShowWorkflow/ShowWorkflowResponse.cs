using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Features.ShowWorkflow;

/// <summary>One instance in full.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Status">Its status in words, for example 'parked (no-progress)'.</param>
/// <param name="Kind">Its state's kind, when the recipe still has the state.</param>
/// <param name="Next">Its state's next text: the one allowed next step.</param>
/// <param name="Signals">The signals it takes now: none while it is held, taken over or ended.</param>
/// <param name="Children">Its child instances.</param>
/// <param name="History">Its events, oldest first.</param>
public sealed record ShowWorkflowResponse(
    WorkflowInstance Instance,
    string Status,
    StateKind? Kind,
    string? Next,
    IReadOnlyList<string> Signals,
    IReadOnlyList<WorkflowInstance> Children,
    IReadOnlyList<HistoryEntry> History);
