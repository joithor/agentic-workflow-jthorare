using System.Globalization;

namespace Aw.Sindri.Core.Context;

/// <summary>One hit from one source, raw: the engine scrubs and cites it.</summary>
/// <param name="Document">Where it is: repo/path for code, the file for notes, the permalink for Linear and ConnectAI.</param>
/// <param name="Line">The matching line, when the source has lines.</param>
/// <param name="Title">A one-line title.</param>
/// <param name="Snippet">A short excerpt that holds a query term.</param>
public sealed record SourceHit(string Document, int? Line, string Title, string Snippet)
{
    /// <summary>The document and line as one locator, 'document:line' or the document alone.</summary>
    public string Locator => Line is { } line ? string.Create(CultureInfo.InvariantCulture, $"{Document}:{line}") : Document;
}
