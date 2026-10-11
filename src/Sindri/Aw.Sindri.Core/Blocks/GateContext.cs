using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>
/// What a gate sees: the instance, its recipe and attempt, the transition it judges (the state it leaves, the state it
/// enters and the signal that asked for it), the facts its steps produced, its registered evidence and the transition's sha.
/// </summary>
/// <param name="Instance">The instance.</param>
/// <param name="Recipe">The instance's recipe.</param>
/// <param name="Item">Its work item.</param>
/// <param name="Attempt">Its attempt, from 1.</param>
/// <param name="State">The state the transition leaves.</param>
/// <param name="To">The state the transition enters.</param>
/// <param name="On">The signal that asked for the transition.</param>
/// <param name="Facts">The facts produced so far, by name (this signal's outputs included).</param>
/// <param name="Evidence">The evidence registered for the instance.</param>
/// <param name="Sha">The head sha of the transition, if any.</param>
public sealed record GateContext(
    InstanceId Instance,
    RecipeId Recipe,
    ItemId Item,
    int Attempt,
    string State,
    string To,
    string On,
    IReadOnlyDictionary<string, string> Facts,
    IReadOnlyList<EvidenceRef> Evidence,
    HeadSha? Sha);
