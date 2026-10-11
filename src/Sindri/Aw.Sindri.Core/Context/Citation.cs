using System.Globalization;

namespace Aw.Sindri.Core.Context;

/// <summary>Where a result came from: the source, the document and line, and when it was retrieved. Every result has one.</summary>
/// <param name="Source">The source.</param>
/// <param name="Document">The document (repo/path, a file, a permalink).</param>
/// <param name="Line">The line, when the source has lines.</param>
/// <param name="RetrievedAt">When the source answered.</param>
public sealed record Citation(SourceKind Source, string Document, int? Line, DateTimeOffset RetrievedAt)
{
    /// <summary>The document and line as one locator.</summary>
    public string Locator => Line is { } line ? string.Create(CultureInfo.InvariantCulture, $"{Document}:{line}") : Document;
}
