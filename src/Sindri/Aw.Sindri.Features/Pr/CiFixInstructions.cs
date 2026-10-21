using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>What 'step.pr.ci-fix' is told (PR babysitter spec Appendix A.1).</summary>
internal sealed class CiFixInstructions : IAgentStepInstructions
{
    /// <inheritdoc/>
    public BlockId Step { get; } = BlockId.From("step.pr.ci-fix");

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
