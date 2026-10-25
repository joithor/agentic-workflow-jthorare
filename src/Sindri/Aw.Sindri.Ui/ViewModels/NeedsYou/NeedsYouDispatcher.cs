using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Runs a ready action of a Home Needs-you row that is not a decision or approve-all: a resume, a desk complete, a service pause, resume or run.
/// One send per action, once per press, never retried; an action it has no send for says so (<see cref="DispatchOutcome.Unhandled"/>).
/// </summary>
internal sealed class NeedsYouDispatcher
{
    internal NeedsYouDispatcher(ISindriApi api, Func<Uri?> apiUrl, InstanceCommander commands, DeskReporter desk)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(desk);
    }

    /// <summary>Runs the action on the row.</summary>
    /// <param name="entry">The row.</param>
    /// <param name="action">One of its ready actions.</param>
    /// <returns>What became of it.</returns>
    internal Task<NeedsYouDispatch> Run(NeedsYouEntry entry, NeedsYouAction action) => throw new NotSupportedException();
}
