using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>What a gate sees: the instance, its current state, the facts its steps produced, its registered evidence and the transition's sha.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Item">Its work item.</param>
/// <param name="State">The state the transition leaves.</param>
/// <param name="Facts">The facts produced so far, by name (this signal's outputs included).</param>
/// <param name="Evidence">The evidence registered for the instance.</param>
/// <param name="Sha">The head sha of the transition, if any.</param>
public sealed record GateContext(
    InstanceId Instance,
    ItemId Item,
    string State,
    IReadOnlyDictionary<string, string> Facts,
    IReadOnlyList<EvidenceRef> Evidence,
    HeadSha? Sha);
