using System.Text.Json;
using Aw.Sindri.Core.Context;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Context;

/// <summary>
/// Opens the sources an approved profile names: code over the profile repos that exist, notes over an existing
/// directory, Linear when LINEAR_API_KEY is set. ConnectAI is a port in this build (no MCP client yet), so it is always
/// skipped with that reason.
/// </summary>
internal sealed class SourceCatalog(SindriSettings settings, LinearHttp linear, JsonSerializerOptions json) : ISourceCatalog
{
    public IReadOnlyList<SourceOpening> Open(SindriProfile profile) => profile.Context is not { } context
        ? []
        : [.. NamedSources.In(profile).Select(kind => kind switch
        {
            SourceKind.Code => Code(context.Code!, profile.Repos ?? []),   // why: NamedSources names Code only when context.code is set
            SourceKind.Notes => Notes(context.Notes!),   // why: NamedSources names Notes only when context.notes is set
            SourceKind.Linear => Linear(context.Linear!),   // why: NamedSources names Linear only when context.linear is set
            SourceKind.ConnectAi => SourceOpening.Skip(kind, "the ConnectAI MCP read surface isn't wired in this build (the port and its fake are; fix: none yet)"),
            _ => SourceOpening.Skip(kind, "unknown source"),
        })];

    private static SourceOpening Code(CodeContext code, IReadOnlyList<RepoEntry> repos)
    {
        var named = code.Repos is { Count: > 0 } names ? [.. repos.Where(r => names.Contains(r.Name, StringComparer.Ordinal))] : repos;
        var present = named.Where(r => Directory.Exists(r.Path)).Select(r => new CodeRepo(r.Name, r.Path)).ToList();
        return present.Count > 0
            ? SourceOpening.Opened(new GitGrepCodeSource(present))
            : SourceOpening.Skip(SourceKind.Code, $"no profile repo checkout exists ({string.Join(", ", named.Select(r => r.Path))}; fix: correct repos: paths)");
    }

    private SourceOpening Notes(NotesContext notes)
    {
        var path = notes.Path.StartsWith("~/", StringComparison.Ordinal) ? Path.Combine(settings.UserHome, notes.Path[2..]) : notes.Path;
        return Directory.Exists(path)
            ? SourceOpening.Opened(new NotesDirectorySource(path))
            : SourceOpening.Skip(SourceKind.Notes, $"the notes directory {path} doesn't exist (fix: create it or correct context.notes.path)");
    }

    private SourceOpening Linear(LinearContext settingsForLinear) => settings.LinearKey is { } key
        ? SourceOpening.Opened(new LinearSource(linear.Client, LinearSource.Api, key, settingsForLinear, json))
        : SourceOpening.Skip(SourceKind.Linear, "LINEAR_API_KEY is not set (fix: export a read-only Linear personal API key as LINEAR_API_KEY before starting sindri)");
}
