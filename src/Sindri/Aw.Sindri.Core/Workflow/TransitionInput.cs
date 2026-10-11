using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>What <see cref="SignalDecisions.Settle"/> decides from: the instance, its recipe, the transition whose gates passed, the signal, the gate records and the time.</summary>
/// <param name="Instance">The instance before the transition.</param>
/// <param name="Recipe">Its recipe.</param>
/// <param name="Transition">The transition.</param>
/// <param name="Signal">The signal.</param>
/// <param name="Gates">The gates evaluated (all passed).</param>
/// <param name="At">When.</param>
public sealed record TransitionInput(
    WorkflowInstance Instance,
    Recipe Recipe,
    RecipeTransition Transition,
    WorkflowSignal Signal,
    IReadOnlyList<GateRecord> Gates,
    DateTimeOffset At);
