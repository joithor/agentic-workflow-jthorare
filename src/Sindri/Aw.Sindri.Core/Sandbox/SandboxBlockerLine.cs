namespace Aw.Sindri.Core.Sandbox;

/// <summary>A blocker in the owner's words: what Check again, <c lang="text">sindri sandbox check</c> and <c lang="text">sindri doctor</c> print (sandbox spec §8).</summary>
/// <param name="Code">The SND-LAUNCH code.</param>
/// <param name="Reason">The code's reason.</param>
/// <param name="Source">The credential source for -017; null otherwise.</param>
/// <param name="Text">The sentence.</param>
/// <param name="Command">The copyable second line, or null.</param>
/// <param name="State">'gray' or 'wait' when the card is never red; null otherwise.</param>
public sealed record SandboxBlockerLine(string Code, string Reason, string? Source, string Text, string? Command, string? State)
{
    /// <summary>Words a blocker.</summary>
    /// <param name="blocker">The blocker.</param>
    /// <returns>Its line; a pair with no sentence says its code and reason, so nothing is hidden.</returns>
    public static SandboxBlockerLine From(SandboxBlocker blocker)
    {
        ArgumentNullException.ThrowIfNull(blocker);
        var code = blocker.Code.Error.Code;
        var copy = SandboxCopy.For(code, blocker.Reason, blocker.Source, blocker.Values);
        return new SandboxBlockerLine(code, blocker.Reason, blocker.Source, copy?.Text ?? $"{code} ({blocker.Reason})", copy?.Command, copy?.State);
    }
}
