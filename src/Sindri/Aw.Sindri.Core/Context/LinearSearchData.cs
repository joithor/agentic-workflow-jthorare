namespace Aw.Sindri.Core.Context;

/// <summary>The data of a search answer.</summary>
/// <param name="SearchIssues">The matching issues.</param>
public sealed record LinearSearchData(LinearIssuePage SearchIssues);
