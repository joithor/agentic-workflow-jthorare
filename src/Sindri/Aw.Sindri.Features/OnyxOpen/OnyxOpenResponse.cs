namespace Aw.Sindri.Features.OnyxOpen;

/// <summary>The page `sindri onyx open` opens. JSON: `{ url, page }`.</summary>
/// <param name="Url">The address on 127.0.0.1.</param>
/// <param name="Page">`sign-up` while Onyx has no account yet, `connectors` after.</param>
public sealed record OnyxOpenResponse(Uri Url, string Page);
