namespace Aw.Sindri.Core.Context;

/// <summary>Linear's GraphQL answer: data, or errors.</summary>
/// <param name="Data">The data, when the query ran.</param>
/// <param name="Errors">GraphQL errors, when it didn't.</param>
public sealed record LinearSearchAnswer(LinearSearchData? Data = null, IReadOnlyList<LinearError>? Errors = null);
