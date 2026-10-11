using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>The 'pr.draft' command payload.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Item">Its work item.</param>
public sealed record DraftPullRequest(InstanceId Instance, ItemId Item);
