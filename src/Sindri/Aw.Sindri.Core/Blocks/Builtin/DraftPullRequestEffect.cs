using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>Opens a draft pull request: a 'pr.draft' outbox command. An outward action, so only approved recipes may name it.</summary>
/// <param name="json">The package's AwJson options (the payload is in its context).</param>
public sealed class DraftPullRequestEffect(JsonSerializerOptions json) : IEffectBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = new(BlockId.From("effect.pr.draft"), BlockKind.Effect, "Opens or updates a draft pull request", [], [],
        Capability: true, CommandKind: EffectAllowlist.DraftPullRequest);

    /// <inheritdoc/>
    public Result<EffectCommand> Command(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new EffectCommand(EffectAllowlist.DraftPullRequest, Encoding.UTF8.GetString(AwJson.Serialize(new DraftPullRequest(context.Instance, context.Item), json)));
    }
}
