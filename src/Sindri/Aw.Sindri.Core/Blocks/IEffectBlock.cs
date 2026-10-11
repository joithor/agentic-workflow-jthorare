using Aw.Kernel;

namespace Aw.Sindri.Core.Blocks;

/// <summary>An effect: the outbox command a transition emits, committed in the transition's transaction (the engine never acts directly).</summary>
public interface IEffectBlock : IBlock
{
    /// <summary>The command for one transition.</summary>
    /// <param name="context">The transition.</param>
    /// <returns>The command's kind and payload, or an error that refuses the transition.</returns>
    public Result<EffectCommand> Command(EffectContext context);
}
