using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Words a park's sandbox refusal for the status and the needs-you row (sandbox spec §8).</summary>
public static class StatusSandboxes
{
    /// <summary>Words <paramref name="park"/> with the sentence Check again says for its code and reason.</summary>
    /// <param name="park">The refusal a park carries.</param>
    /// <returns>The sandbox part of a status or a row.</returns>
    public static StatusSandbox Of(SandboxPark park) => throw new NotSupportedException("the sandbox part of a park is not worded yet");
}
