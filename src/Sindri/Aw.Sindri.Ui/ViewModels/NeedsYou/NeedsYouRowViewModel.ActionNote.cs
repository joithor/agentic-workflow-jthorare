namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What an action pressed on a Home row said (<see cref="NeedsYouRowViewModel"/>, continued): sending, sent, or the refusal as served.</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>What the last action pressed on the row said (sending, sent, the refusal as served); empty before one was pressed.</summary>
    public string ActionNote { get; private set; } = "";

    /// <summary>Says what became of an action pressed on the row.</summary>
    /// <param name="note">The words; empty clears them.</param>
    internal void SetActionNote(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (!string.Equals(ActionNote, note, StringComparison.Ordinal))
        {
            ActionNote = note;
            OnChanged(nameof(ActionNote));
            OnChanged(nameof(Notice));
            OnChanged(nameof(HasNotice));
        }
    }
}
