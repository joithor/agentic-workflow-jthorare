using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Profile;

public sealed class ContextProfileTests : IDisposable
{
    private const string Base = "schemaVersion: 1\nuser: thor\nrepos:\n  - { name: aw, path: /repos/aw }\n";
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task A_context_section_loads_typed_with_its_defaults_and_sorted_names()
    {
        _files.Write(Base + "context:\n  code: { repos: [aw] }\n  notes: { path: ~/notes }\n  linear: { teams: [OPS, ENG] }\n  connectai: { server: connectai, tool: search }\n");

        var context = (await _files.Source.Load(Token)).Value.Profile.Context!;   // why: the YAML above has a context section

        Assert.Equal(["aw"], context.Code!.Repos);   // why: code is set above
        Assert.Equal("~/notes", context.Notes!.Path);   // why: notes is set above
        Assert.Equal(["ENG", "OPS"], context.Linear!.Teams);   // why: linear is set above
        Assert.Equal(("connectai", "search"), (context.ConnectAi!.Server, context.ConnectAi.Tool));   // why: connectai is set above
        Assert.Equal(10, context.Limit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_profile_without_context_keeps_the_hash_it_had_before_the_key_existed()
    {
        _files.Write(Base);

        var hash = (await _files.Source.Load(Token)).Value.Hash;

        // The S1 canonical JSON of this profile, before the context key existed, hashed: the key joins version 1 without
        // changing any existing approval.
        Assert.Equal(ProfileHashingOfS1Canonical, hash.Value);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("context:\n  limit: 0\n", "context.limit: must be 1 to 50")]
    [InlineData("context:\n  code: { repos: [nope] }\n", "context.code.repos: 'nope' is not a profile repo")]
    [InlineData("context:\n  notes: { path: notes }\n", "context.notes.path: 'notes' must be an absolute path")]
    [InlineData("context:\n  connectai: { server: '', tool: search }\n", "context.connectai: server and tool must be non-empty")]
    [InlineData("context:\n  linear: { teams: [''] }\n", "context.linear.teams: team keys must be non-empty")]
    [InlineData("context:\n  linear: { teams: [' ENG'] }\n", "without surrounding spaces")]
    [InlineData("context:\n  slack: {}\n", "'slack' is not a profile key here (allowed: code, notes, linear, connectai, limit)")]
    public async Task A_bad_context_section_names_the_key_and_the_fix(string context, string expected)
    {
        _files.Write(Base + context);

        var error = (await _files.Source.Load(Token)).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
        Assert.Contains("fix:", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_source_key_with_no_value_is_refused_with_the_fix()
    {
        _files.Write(Base + "context:\n  code:\n  notes: { path: /notes }\n");

        var error = (await _files.Source.Load(Token)).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains("context.code: has no value; give it a mapping (code: {} turns the source on with its defaults)", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_empty_list_hashes_the_same_as_none()
    {
        _files.Write(Base + "context:\n  code: { repos: [] }\n  linear: { teams: [] }\n");
        var empty = (await _files.Source.Load(Token)).Value.Hash;
        _files.Write(Base + "context:\n  code: {}\n  linear: {}\n");
        var none = (await _files.Source.Load(Token)).Value.Hash;

        Assert.Equal(none, empty);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_approved_context_section_reads_back_from_its_snapshot()
    {
        _files.Write(Base + "context:\n  notes: { path: /notes }\n");
        var live = (await _files.Source.Load(Token)).Value;

        _ = await _files.Approvals.Save(live, Token);
        var approved = (await _files.Approvals.Latest(Token)).Value;

        Assert.Equal(live.Hash, approved!.Hash);   // why: it was saved just above
        Assert.Equal("/notes", approved.Profile.Context!.Notes!.Path);   // why: the saved profile names notes
        Assert.Null(approved.Profile.Context.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Code_needs_a_profile_repo()
    {
        _files.Write("schemaVersion: 1\nuser: thor\ncontext:\n  code: {}\n");

        var error = (await _files.Source.Load(Token)).Error;

        Assert.Contains("context.code: needs at least one profile repo", error.Message, StringComparison.Ordinal);
    }

    private static string ProfileHashingOfS1Canonical =>
        Core.Profile.ProfileHashing.Of("""{"schemaVersion":1,"user":"thor","tenant":"local","repos":[{"name":"aw","path":"/repos/aw"}],"services":{},"api":{"port":7410}}"""u8.ToArray()).Value;
}
