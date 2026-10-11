namespace Aw.Sindri.Infrastructure.Context;

/// <summary>A profile repo the code source searches.</summary>
/// <param name="Name">Its name (the first segment of every hit's document).</param>
/// <param name="Path">Its checkout.</param>
internal sealed record CodeRepo(string Name, string Path);
