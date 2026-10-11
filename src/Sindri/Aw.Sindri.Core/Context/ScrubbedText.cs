namespace Aw.Sindri.Core.Context;

/// <summary>Scrubbed text.</summary>
/// <param name="Text">The text, with secrets replaced.</param>
/// <param name="Redacted">True when something was replaced or the whole text withheld.</param>
public sealed record ScrubbedText(string Text, bool Redacted);
