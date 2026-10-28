using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Words a park's sandbox refusal for the status and the needs-you row (sandbox spec §8).</summary>
public static class StatusSandboxes
{
    /// <summary>Words <paramref name="park"/> when there is one.</summary>
    /// <param name="park">The refusal a park carries, or null.</param>
    /// <returns>The sandbox part, or null when there is no refusal.</returns>
    public static StatusSandbox? OrNull(SandboxPark? park) => park is null ? null : Of(park);

    /// <summary>Words <paramref name="park"/> with the sentence Check again says for its code and reason.</summary>
    /// <param name="park">The refusal a park carries.</param>
    /// <returns>The sandbox part of a status or a row; a pair with no sentence says its code and reason, so nothing is hidden.</returns>
    public static StatusSandbox Of(SandboxPark park)
    {
        ArgumentNullException.ThrowIfNull(park);
        var copy = SandboxCopy.For(park.Code, park.Reason ?? string.Empty, park.Source);
        var text = copy?.Text ?? (park.Reason is null ? park.Code : $"{park.Code} ({park.Reason})");
        return new StatusSandbox(park.Code, park.Reason, park.Source, text, copy?.Command, copy?.State);
    }
}
