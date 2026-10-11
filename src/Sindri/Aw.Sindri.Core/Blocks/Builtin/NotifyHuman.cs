using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>The 'notify.human' command payload.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Item">Its work item.</param>
/// <param name="Text">What to tell the human.</param>
public sealed record NotifyHuman(InstanceId Instance, ItemId Item, string Text);
