using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.StartWorkflow;

/// <summary>Start a workflow instance of a recipe for a work item (factory addendum §17.28.1). The same (recipe, item, attempt) again is a no-op.</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Item">The work item.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="BaseSha">The base commit, for 'kind@base' evidence.</param>
public sealed record StartWorkflowRequest(RecipeId Recipe, ItemId Item, int Attempt = 1, HeadSha? BaseSha = null);
