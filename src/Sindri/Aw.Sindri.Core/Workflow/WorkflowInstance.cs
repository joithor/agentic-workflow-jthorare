using System.Text.Json.Serialization;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// A workflow instance (factory addendum §17.28.1): the recipe id and hash it is pinned to, the item and attempt, the
/// current state and any hold, the loop counters, the budget spent, the facts its steps produced, the registered
/// evidence, its timer, a version for optimistic concurrency, and the tenant and ring keys (§17.20). It is the fold of
/// its events (<see cref="InstanceFold"/>).
/// </summary>
/// <param name="Id">The instance id.</param>
/// <param name="Recipe">The recipe.</param>
/// <param name="RecipeHash">The recipe hash it started with; a changed recipe doesn't apply to it.</param>
/// <param name="Item">The work item.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Parent">The parent instance, for a child workflow.</param>
/// <param name="Root">The root of its workflow tree (itself for a top-level instance): every event of the tree shares the root's correlation.</param>
/// <param name="Tenant">The tenant key.</param>
/// <param name="Ring">The ring.</param>
/// <param name="Version">Rises by one per event.</param>
/// <param name="State">The recipe state it is in (kept while parked, escalated or taken over).</param>
/// <param name="Hold">A park or escalation, if any.</param>
/// <param name="TakenOver">True while the human has taken it over (timers paused).</param>
/// <param name="Ended">How it ended, if it has.</param>
/// <param name="Facts">The facts its steps produced, by name.</param>
/// <param name="Loops">Rounds per loop.</param>
/// <param name="Findings">The last round's findings per loop, for the no-progress rule.</param>
/// <param name="Spent">Tokens and turns spent by the instance.</param>
/// <param name="StateSpent">Tokens and turns spent since it entered its state.</param>
/// <param name="Extra">Budget the human granted on unpark, on top of the recipe's.</param>
/// <param name="Evidence">Evidence registered for it.</param>
/// <param name="Deadline">When the state's timer fires, if it runs.</param>
/// <param name="Paused">The timer's remaining time while taken over.</param>
/// <param name="Retries">Timeout retries in the current state.</param>
/// <param name="BaseSha">The base commit, for 'kind@base' evidence.</param>
/// <param name="StartedAt">When it started.</param>
/// <param name="EnteredAt">When it entered its state.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record WorkflowInstance(
    InstanceId Id,
    RecipeId Recipe,
    RecipeHash RecipeHash,
    ItemId Item,
    int Attempt,
    InstanceId? Parent,
    InstanceId Root,
    string Tenant,
    int Ring,
    long Version,
    string State,
    InstanceHold? Hold,
    bool TakenOver,
    InstanceEnd? Ended,
    IReadOnlyDictionary<string, string> Facts,
    IReadOnlyDictionary<string, int> Loops,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Findings,
    Spend Spent,
    Spend StateSpent,
    Spend Extra,
    IReadOnlyList<EvidenceRef> Evidence,
    DateTimeOffset? Deadline,
    TimeSpan? Paused,
    int Retries,
    HeadSha? BaseSha,
    DateTimeOffset StartedAt,
    DateTimeOffset EnteredAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>The status as a word.</summary>
    [JsonIgnore]
    public InstanceStatus Status => (Ended, TakenOver, Hold?.Kind) switch
    {
        (InstanceEnd.Completed, _, _) => InstanceStatus.Done,
        (InstanceEnd.Cancelled, _, _) => InstanceStatus.Cancelled,
        (_, true, _) => InstanceStatus.TakenOver,
        (_, _, HoldKind.Parked) => InstanceStatus.Parked,
        (_, _, HoldKind.Escalated) => InstanceStatus.Escalated,
        _ => InstanceStatus.Active,
    };

    /// <summary>True while it can take signals: not ended, held or taken over.</summary>
    [JsonIgnore]
    public bool Runs => Status is InstanceStatus.Active;
}
