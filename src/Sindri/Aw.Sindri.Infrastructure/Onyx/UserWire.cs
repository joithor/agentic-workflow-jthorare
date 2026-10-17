namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One row of `GET /api/manage/users/accepted/all` (FullUserSnapshot).</summary>
/// <param name="Email">The user's email.</param>
/// <param name="Groups">The groups it is in, default groups included.</param>
internal sealed record UserWire(string? Email = null, IReadOnlyList<GroupWire>? Groups = null);
