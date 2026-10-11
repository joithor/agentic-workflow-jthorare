using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>What an effect sees: the instance and the transition that emits it.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Item">Its work item.</param>
/// <param name="Recipe">Its recipe.</param>
/// <param name="From">The state it leaves.</param>
/// <param name="To">The state it enters.</param>
/// <param name="Facts">The instance's facts after the transition.</param>
public sealed record EffectContext(InstanceId Instance, ItemId Item, RecipeId Recipe, string From, string To, IReadOnlyDictionary<string, string> Facts);
