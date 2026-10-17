using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>Onyx's `GET /api/me` reply (UserInfo); only what `onyx up` reads.</summary>
/// <param name="Email">The account's email.</param>
/// <param name="EffectivePermissions">The permissions it holds; `admin` is the admin panel's.</param>
/// <param name="AccountType">STANDARD, SERVICE_ACCOUNT and so on.</param>
internal sealed record MeWire(
    string? Email = null,
    [property: JsonPropertyName("effective_permissions")] IReadOnlyList<string>? EffectivePermissions = null,
    [property: JsonPropertyName("account_type")] string? AccountType = null);
