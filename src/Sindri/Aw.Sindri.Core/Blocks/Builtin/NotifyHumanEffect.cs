using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks.Builtin;

/// <summary>Tells the human a transition happened: a 'notify.human' outbox command.</summary>
/// <param name="json">The package's AwJson options (the payload is in its context).</param>
public sealed class NotifyHumanEffect(JsonSerializerOptions json) : IEffectBlock
{
    /// <inheritdoc/>
    public BlockSpec Spec { get; } = new(BlockId.From("effect.notify.human"), BlockKind.Effect, "Tells the human about the transition", [], [],
        CommandKind: EffectAllowlist.NotifyHuman);

    /// <inheritdoc/>
    public Result<EffectCommand> Command(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var payload = new NotifyHuman(context.Instance, context.Item, $"{context.Recipe.Value} {context.Item.Value}: {context.From} -> {context.To}");
        return new EffectCommand(EffectAllowlist.NotifyHuman, Encoding.UTF8.GetString(AwJson.Serialize(payload, json)));
    }
}
