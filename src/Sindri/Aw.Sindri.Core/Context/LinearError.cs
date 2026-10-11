namespace Aw.Sindri.Core.Context;

/// <summary>A GraphQL error.</summary>
/// <param name="Message">Its message.</param>
public sealed record LinearError(string Message);
