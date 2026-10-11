namespace Aw.Sindri.Testing.Context;

/// <summary>A document a contract seeds a source with: a name (file name or key), a title and a body.</summary>
/// <param name="Name">A file-name-safe name, unique in the seed.</param>
/// <param name="Title">A one-line title.</param>
/// <param name="Body">The text, one or more lines.</param>
public sealed record SeedDocument(string Name, string Title, string Body);
