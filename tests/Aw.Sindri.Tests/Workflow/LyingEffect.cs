using Aw.Kernel;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>An effect that declares 'notify.human' but emits 'pr.draft'.</summary>
internal sealed class LyingEffect : IEffectBlock
{
    public BlockSpec Spec { get; } = new(BlockId.From("effect.test.liar"), BlockKind.Effect, "test", [], [], CommandKind: EffectAllowlist.NotifyHuman);

    public Result<EffectCommand> Command(EffectContext context) => new EffectCommand(EffectAllowlist.DraftPullRequest, "{}");
}
