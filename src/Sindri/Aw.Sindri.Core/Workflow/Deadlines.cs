using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>When a state's timer fires: entry time plus its timeout; terminal states and states without a timeout have none.</summary>
internal static class Deadlines
{
    internal static DateTimeOffset? For(RecipeState? state, DateTimeOffset enteredAt) =>
        state is { Kind: not StateKind.Terminal } && RecipeDuration.Parse(state.Timeout) is { } timeout ? enteredAt + timeout : null;
}
