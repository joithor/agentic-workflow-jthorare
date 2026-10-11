using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks the JSON shape can't express for the profile's context: section, returning its canonical form.</summary>
public static class ContextRules
{
    /// <summary>Checks <paramref name="context"/> against the profile's repos.</summary>
    /// <param name="context">The context section.</param>
    /// <param name="repos">The profile's repos.</param>
    /// <returns>The canonical section (names sorted), or SND-PROFILE-002 naming the key path.</returns>
    public static Result<ContextSettings> Check(ContextSettings context, IReadOnlyList<RepoEntry> repos)
    {
        if (context.Limit is < 1 or > ContextSettings.MaxLimit)
        {
            return Invalid("context.limit", string.Create(CultureInfo.InvariantCulture, $"must be 1 to {ContextSettings.MaxLimit}"));
        }

        var problem = Code(context.Code, repos) ?? Notes(context.Notes) ?? Linear(context.Linear) ?? Mcp(context.ConnectAi);
        if (problem is not null)
        {
            return problem;
        }

        return Canonical(context);
    }

    private static Error? Code(CodeContext? code, IReadOnlyList<RepoEntry> repos)
    {
        if (code is null)
        {
            return null;
        }

        if (repos.Count == 0)
        {
            return Invalid("context.code", "needs at least one profile repo (add it under repos:)");
        }

        var unknown = (code.Repos ?? []).FirstOrDefault(name => !repos.Any(r => string.Equals(r.Name, name, StringComparison.Ordinal)));
        return unknown is null ? null : Invalid("context.code.repos", $"'{unknown}' is not a profile repo (repos: {string.Join(", ", repos.Select(r => r.Name))})");
    }

    private static Error? Notes(NotesContext? notes) =>
        notes is null || notes.Path.StartsWith('/') || notes.Path.StartsWith("~/", StringComparison.Ordinal)
            ? null
            : Invalid("context.notes.path", $"'{notes.Path}' must be an absolute path or start with ~/");

    private static Error? Linear(LinearContext? linear) =>
        linear?.Teams is { } teams && teams.Any(string.IsNullOrWhiteSpace) ? Invalid("context.linear.teams", "team keys must be non-empty text") : null;

    private static Error? Mcp(McpContext? mcp) =>
        mcp is not null && (string.IsNullOrWhiteSpace(mcp.Server) || string.IsNullOrWhiteSpace(mcp.Tool))
            ? Invalid("context.connectai", "server and tool must be non-empty text")
            : null;

    private static ContextSettings Canonical(ContextSettings context) => context with
    {
        Code = context.Code is { Repos: { } repos } code ? code with { Repos = [.. repos.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] } : context.Code,
        Linear = context.Linear is { Teams: { } teams } linear ? linear with { Teams = [.. teams.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)] } : context.Linear,
    };

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
