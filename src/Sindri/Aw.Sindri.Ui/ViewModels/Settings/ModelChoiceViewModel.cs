using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>
/// Settings' read-only model-choice line (model-pick spec 8): on, off, or "the profile has a change waiting for your approval", from what Sindri serves in the status (the app derives none of it),
/// the sentence under it, and a Copy of the three lines that change it (the profile file's path, 'sindri profile validate', 'sindri profile approve'). There is no edit verb.
/// </summary>
internal sealed class ModelChoiceViewModel : ObservableObject
{
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;

    /// <summary>The line over the status's profile part, with a clipboard and a clock for Copy's "Copied".</summary>
    /// <param name="clipboard">Where Copy puts the three lines.</param>
    /// <param name="clock">The clock for "Copied".</param>
    internal ModelChoiceViewModel(IClipboard clipboard, TimeProvider clock)
    {
        (_clipboard, _clock) = (clipboard, clock);
    }

    /// <summary>The heading.</summary>
    public string Title => throw new NotSupportedException(_clipboard.GetType().Name + _clock.GetType().Name);

    /// <summary>The line in words.</summary>
    public string Line => throw new NotSupportedException(_clipboard.GetType().Name + _clock.GetType().Name);

    /// <summary>The sentence under the line.</summary>
    public string Sentence => throw new NotSupportedException(_clipboard.GetType().Name + _clock.GetType().Name);

    /// <summary>The three lines Copy puts on the clipboard.</summary>
    public string CopyText => throw new NotSupportedException(_clipboard.GetType().Name + _clock.GetType().Name);

    /// <summary>"Copy", or "Copied" for 1.5 s after a copy.</summary>
    public string CopyLabel => throw new NotSupportedException(_clipboard.GetType().Name + _clock.GetType().Name);

    /// <summary>A status arrived: the line follows its profile part.</summary>
    /// <param name="profile">The status's profile part.</param>
    internal void Show(ProfileView profile) => throw new NotSupportedException();

    /// <summary>Copies the three lines.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal Task Copy(CancellationToken cancellationToken) => throw new NotSupportedException();
}
