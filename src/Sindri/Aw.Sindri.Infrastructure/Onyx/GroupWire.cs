namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>A user group as the user list names it (UserGroupInfo).</summary>
/// <param name="Id">The group's id.</param>
/// <param name="Name">Its name; the default admin group is `Admin`.</param>
internal sealed record GroupWire(int Id = 0, string? Name = null);
