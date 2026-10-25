using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// Runs a ready action of a Home Needs-you row that is not a decision or approve-all: a resume, a desk complete, a service pause, resume or run.
/// One send per action, once per press, never retried; an action it has no send for says so (<see cref="DispatchOutcome.Unhandled"/>).
/// </summary>
internal sealed class NeedsYouDispatcher
{
    private const string InstanceKind = "instance";
    private const string ServiceKind = "service";
    private const string ResumeAction = "ApplyCommand:answer-resume";
    private const string CompleteAction = "CompleteStep";
    private const string PauseAction = "PauseService";
    private const string ResumeServiceAction = "ResumeService";
    private const string RunAction = "RunService";
    private const string ApproveOutcome = "approve";

    private readonly ISindriApi _api;
    private readonly Func<Uri?> _apiUrl;
    private readonly InstanceCommander _commands;
    private readonly DeskReporter _desk;
    private readonly HashSet<string> _services = new(StringComparer.Ordinal);

    internal NeedsYouDispatcher(ISindriApi api, Func<Uri?> apiUrl, InstanceCommander commands, DeskReporter desk)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(desk);
        (_api, _apiUrl, _commands, _desk) = (api, apiUrl, commands, desk);
    }

    /// <summary>Runs the action on the row.</summary>
    /// <param name="entry">The row.</param>
    /// <param name="action">One of its ready actions.</param>
    /// <returns>What became of it.</returns>
    internal Task<NeedsYouDispatch> Run(NeedsYouEntry entry, NeedsYouAction action)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(action);
        var id = action.CatalogId;
        if (SubjectOf(entry, InstanceKind) is { } instance)
        {
            if (string.Equals(id, ResumeAction, StringComparison.Ordinal))
            {
                return Resume(instance);
            }

            if (string.Equals(id, CompleteAction, StringComparison.Ordinal))
            {
                return Complete(instance);
            }
        }

        if (SubjectOf(entry, ServiceKind) is { } service && (string.Equals(id, PauseAction, StringComparison.Ordinal) || string.Equals(id, ResumeServiceAction, StringComparison.Ordinal) || string.Equals(id, RunAction, StringComparison.Ordinal)))
        {
            return Service(id, service);
        }

        return Task.FromResult(new NeedsYouDispatch(DispatchOutcome.Unhandled, Labels.RowNotAvailable(action.Cli)));
    }

    private static string? SubjectOf(NeedsYouEntry entry, string kind) =>
        entry.Subject is { } subject && string.Equals(subject.Kind, kind, StringComparison.Ordinal) && subject.Id.Length > 0 ? subject.Id : null;

    private async Task<NeedsYouDispatch> Resume(string instance) =>
        Reply(await _commands.Send(ApplyCommandBody.Resume(instance)).ConfigureAwait(true));

    // The desk report is sent only for the outcome that needs nothing more from the person (approve, as the desk recommends it); any other
    // outcome, or none, opens the row's sheet where the action bar asks for the reason or the choice.
    private async Task<NeedsYouDispatch> Complete(string instance)
    {
        if (await _desk.Load(instance).ConfigureAwait(true) is not { } target)
        {
            return new NeedsYouDispatch(DispatchOutcome.Refused, Labels.CompleteUnreadable);
        }

        if (target.Recommended is not { } recommended || !string.Equals(recommended.Outcome, ApproveOutcome, StringComparison.OrdinalIgnoreCase))
        {
            return new NeedsYouDispatch(DispatchOutcome.OpenSheet, Labels.CompleteNeedsChoice);
        }

        return Reply(await _desk.Complete(target, ApproveOutcome, answers: null).ConfigureAwait(true), Labels.CompleteSent);
    }

    private async Task<NeedsYouDispatch> Service(string action, string name)
    {
        if (_apiUrl() is not { } url || !_services.Add(name))
        {
            return Busy();
        }

        try
        {
            if (string.Equals(action, PauseAction, StringComparison.Ordinal))
            {
                return Reply(await _api.PauseService(url, name, CancellationToken.None).ConfigureAwait(true));
            }

            return string.Equals(action, ResumeServiceAction, StringComparison.Ordinal)
                ? Reply(await _api.ResumeService(url, name, CancellationToken.None).ConfigureAwait(true))
                : Reply(await _api.RunService(url, name, CancellationToken.None).ConfigureAwait(true));
        }
        finally
        {
            _ = _services.Remove(name);
        }
    }

    // A null answer is a send that did not go out: one for this target was in flight, or no endpoint is known.
    private static NeedsYouDispatch Reply<T>(Result<T>? answer, string sent = Labels.RowSent) => answer switch
    {
        null => Busy(),
        { IsError: true } refused => new NeedsYouDispatch(DispatchOutcome.Refused, refused.Error.Message),
        _ => new NeedsYouDispatch(DispatchOutcome.Sent, sent),
    };

    private static NeedsYouDispatch Busy() => new(DispatchOutcome.Busy, Labels.RowSending);
}
