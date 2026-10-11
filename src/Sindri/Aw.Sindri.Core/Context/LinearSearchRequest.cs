namespace Aw.Sindri.Core.Context;

/// <summary>The GraphQL request the Linear source sends (Linear's searchIssues; read-only).</summary>
/// <param name="Query">The GraphQL query text.</param>
/// <param name="Variables">Its variables.</param>
public sealed record LinearSearchRequest(string Query, LinearSearchVariables Variables);
