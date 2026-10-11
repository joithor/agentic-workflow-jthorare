namespace Aw.Sindri.Core.Context;

/// <summary>An issue as the Linear source reads it.</summary>
/// <param name="Identifier">Its key, for example ENG-482.</param>
/// <param name="Title">Its title.</param>
/// <param name="Url">Its permalink.</param>
/// <param name="Description">Its description (Markdown), when it has one.</param>
public sealed record LinearIssue(string Identifier, string Title, string Url, string? Description = null);
