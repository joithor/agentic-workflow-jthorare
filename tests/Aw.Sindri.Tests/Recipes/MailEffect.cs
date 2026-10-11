using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>An effect with a command kind that isn't on the allowlist.</summary>
internal sealed class MailEffect : IEffectBlock
{
    public BlockSpec Spec { get; } = new(BlockId.From("effect.test.mail"), BlockKind.Effect, "test", [], [], CommandKind: "mail.send");

    public Result<EffectCommand> Command(EffectContext context) => new EffectCommand("mail.send", "{}");
}
