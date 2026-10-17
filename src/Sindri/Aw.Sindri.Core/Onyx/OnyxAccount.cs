namespace Aw.Sindri.Core.Onyx;

/// <summary>Who `GET /api/me` says a credential belongs to: only what `onyx up` needs to decide.</summary>
/// <param name="Email">The account's email (a service account has a generated one).</param>
/// <param name="IsAdmin">Whether its effective permissions include `admin`.</param>
/// <param name="IsServiceAccount">Whether it is a service account (an API key's user) rather than a person.</param>
public sealed record OnyxAccount(string Email, bool IsAdmin, bool IsServiceAccount);
