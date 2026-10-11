using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>What a new instance starts from.</summary>
/// <param name="Recipe">The recipe.</param>
/// <param name="Item">The work item.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Parent">The parent, for a child workflow.</param>
/// <param name="BaseSha">The base commit, if known.</param>
/// <param name="Root">The root of the parent's tree, for a child workflow.</param>
internal sealed record StartSpec(ValidatedRecipe Recipe, ItemId Item, int Attempt, InstanceId? Parent, HeadSha? BaseSha, InstanceId? Root = null);
