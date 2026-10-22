using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Commands.Scrub;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands.Scrub;

/// <summary>
/// 'sindri scrub' end to end over a real temp git repository: standard input, the staged index, the pre-commit hook. The git
/// calls run with the user's git config kept out (no global core.hooksPath), and the secrets are built from parts so this
/// file holds none.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ScrubVerbTests : IDisposable
{
    private const string Binary = "/opt/sindri/sindri";
    private static readonly string _token = "ghp_" + new string('a', 36);
    private static readonly Dictionary<string, string?> _gitEnvironment = new()
    {
        ["GIT_CONFIG_GLOBAL"] = "/dev/null",
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_AUTHOR_NAME"] = "t",
        ["GIT_AUTHOR_EMAIL"] = "t@example.invalid",
        ["GIT_COMMITTER_NAME"] = "t",
        ["GIT_COMMITTER_EMAIL"] = "t@example.invalid",
    };

    private readonly TempHome _temp = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _temp.Dispose();
        _out.Dispose();
        _error.Dispose();
    }

    private string Repo => Path.Combine(_temp.Path, "repo");

    private static GitAnswer Git(string directory, IReadOnlyList<string> arguments) => ProcessGit.Run(directory, arguments, _gitEnvironment);

    private string NewRepo()
    {
        _ = Directory.CreateDirectory(Repo);
        Assert.True(Git(Repo, ["init", "-q"]).Ok);
        return Repo;
    }

    private void Stage(string name, string content)
    {
        File.WriteAllText(Path.Combine(Repo, name), content);
        Assert.True(Git(Repo, ["add", name]).Ok);
    }

    private async Task<int> Run(string directory, string stdin, params string[] args)
    {
        var io = new ScrubIo(new StringReader(stdin), new Output(_out, _error, SliceHarness.Json), _error, directory);
        return await new ScrubVerb(io, Git, Redactor.Default, Binary).Run(args);
    }

    [Fact]
    public async Task Text_on_stdin_passes_through_with_a_known_secret_replaced_and_reported()
    {
        var code = await Run(_temp.Path, $"token {_token} end\n");

        Assert.Equal((0, "token [REDACTED:github-token] end\n"), (code, _out.ToString()));
        Assert.Equal("scrubbed 1 hit(s)\n", _error.ToString());
        Assert.DoesNotContain(_token, _out.ToString() + _error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clean_text_on_stdin_passes_through_unchanged_and_says_nothing()
    {
        var code = await Run(_temp.Path, "just text\n");

        Assert.Equal((0, "just text\n", string.Empty), (code, _out.ToString(), _error.ToString()));
    }

    [Fact]
    public async Task Json_wraps_the_text_with_each_hit_s_rule_and_span_and_never_the_value()
    {
        var code = await Run(_temp.Path, $"a {_token}", "--json");

        using var document = JsonDocument.Parse(_out.ToString());
        var hit = document.RootElement.GetProperty("hits")[0];
        Assert.Equal(0, code);
        Assert.Equal("a [REDACTED:github-token]", document.RootElement.GetProperty("text").GetString());
        Assert.Equal(("github-token", 2, 2 + _token.Length), (hit.GetProperty("rule").GetString(), hit.GetProperty("start").GetInt32(), hit.GetProperty("end").GetInt32()));
        Assert.DoesNotContain(_token, _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staged_with_a_secret_is_refused_with_its_file_line_and_rule()
    {
        _ = NewRepo();
        Stage("clean.txt", "hello\n");
        Stage("app.env", $"one\ntwo\nGH={_token}\n");

        var code = await Run(Repo, string.Empty, "--staged");

        Assert.Equal(1, code);
        Assert.Contains("SND-SCRUB-002 refused: 1 likely secret(s) in staged changes:", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("  app.env:3 github-token\n", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_token, _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staged_with_nothing_secret_exits_0()
    {
        _ = NewRepo();
        Stage("a.txt", "hello\n");

        var code = await Run(Repo, string.Empty, "--staged");

        Assert.Equal((0, "No secrets in staged changes.\n"), (code, _out.ToString()));
    }

    [Fact]
    public async Task Staged_reads_the_index_not_the_working_tree()
    {
        _ = NewRepo();
        Stage("a.txt", "hello\n");
        File.WriteAllText(Path.Combine(Repo, "a.txt"), $"hello\n{_token}\n");   // only in the working tree
        File.WriteAllText(Path.Combine(Repo, "untracked.txt"), _token);

        var code = await Run(Repo, string.Empty, "--staged");

        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Staged_scans_only_what_the_commit_adds_not_what_a_file_already_holds()
    {
        _ = NewRepo();
        Stage("a.txt", $"{_token}\n");
        Assert.True(Git(Repo, ["commit", "-q", "-m", "old"]).Ok);
        Stage("a.txt", $"{_token}\nmore\n");

        var code = await Run(Repo, string.Empty, "--staged");

        Assert.Equal(0, code);   // the old line is context to this change, not an addition
    }

    [Fact]
    public async Task Staged_finds_a_key_split_over_lines_and_names_its_first_line()
    {
        _ = NewRepo();
        Stage("k.pem", "x\n-----BEGIN RSA " + "PRIVATE KEY-----\nMIIEabc\n-----END RSA " + "PRIVATE KEY-----\n");

        var code = await Run(Repo, string.Empty, "--staged");

        Assert.Equal(1, code);
        Assert.Contains("  k.pem:2 private-key\n", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Staged_json_lists_the_hits_and_exits_1()
    {
        _ = NewRepo();
        Stage("a.txt", $"{_token}\n");

        var code = await Run(Repo, string.Empty, "--staged", "--json");

        using var document = JsonDocument.Parse(_out.ToString());
        Assert.Equal(1, code);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        var hit = document.RootElement.GetProperty("hits")[0];
        Assert.Equal(("a.txt", 1, "github-token"), (hit.GetProperty("file").GetString(), hit.GetProperty("line").GetInt32(), hit.GetProperty("rule").GetString()));
    }

    [Fact]
    public async Task Staged_json_when_clean_is_ok_with_no_hits()
    {
        _ = NewRepo();
        Stage("a.txt", "x\n");

        var code = await Run(Repo, string.Empty, "--staged", "--json");

        Assert.Equal((0, "{\"ok\":true,\"hits\":[]}\n"), (code, _out.ToString()));
    }

    [Fact]
    public async Task Staged_outside_a_repo_is_an_error_not_a_pass()
    {
        var code = await Run(_temp.Path, string.Empty, "--staged");

        Assert.Equal(2, code);
        Assert.StartsWith("SND-SCRUB-004 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_writes_an_executable_hook_that_calls_the_binary_and_a_second_run_changes_nothing()
    {
        _ = NewRepo();

        var first = await Run(Repo, string.Empty, "--install-pre-commit");
        var hook = Path.Combine(Repo, ".git", "hooks", "pre-commit");
        var text = File.ReadAllText(hook);
        var second = await Run(Repo, string.Empty, "--install-pre-commit", "--json");

        Assert.Equal((0, 0), (first, second));
        Assert.Equal(PreCommitHook.Text(Binary), text);
        Assert.True((File.GetUnixFileMode(hook) & UnixFileMode.UserExecute) != 0);
        Assert.EndsWith("\"changed\":false}\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_takes_a_repo_path_and_replaces_the_hook_the_typescript_sindri_wrote()
    {
        _ = NewRepo();
        var hook = Path.Combine(Repo, ".git", "hooks", "pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\n# sindri-pre-commit v2\n\"$SINDRI\" shape --record --staged || true\n");

        var code = await Run(_temp.Path, string.Empty, "--install-pre-commit", "--repo", Repo);

        Assert.Equal(0, code);
        Assert.Equal(PreCommitHook.Text(Binary), File.ReadAllText(hook));
    }

    [Fact]
    public async Task Install_refuses_a_foreign_hook_and_leaves_it_alone_saying_so()
    {
        _ = NewRepo();
        var hook = Path.Combine(Repo, ".git", "hooks", "pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        File.WriteAllText(hook, "#!/bin/sh\nnpm test\n");

        var code = await Run(Repo, string.Empty, "--install-pre-commit");

        Assert.Equal(2, code);
        Assert.Equal("#!/bin/sh\nnpm test\n", File.ReadAllText(hook));
        Assert.StartsWith("SND-SCRUB-003 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("already exists and is not sindri's", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Install_refuses_to_write_into_a_core_hooksPath_directory()
    {
        _ = NewRepo();
        var shared = Path.Combine(_temp.Path, "shared-hooks");
        Assert.True(Git(Repo, ["config", "core.hooksPath", shared]).Ok);

        var code = await Run(Repo, string.Empty, "--install-pre-commit");

        Assert.Equal(2, code);
        Assert.False(Directory.Exists(shared));
        Assert.Contains("core.hooksPath", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_installed_hook_blocks_a_commit_with_a_secret_and_lets_a_clean_one_through()
    {
        // The real binary stands in as a script that reports what the hook asked of it.
        var fake = Path.Combine(_temp.Path, "sindri");
        File.WriteAllText(fake, "#!/bin/sh\n[ \"$1 $2\" = \"scrub --staged\" ] && echo ran >> \"$0.log\"\nexit ${FAKE_EXIT:-0}\n");
        File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _ = NewRepo();
        var io = new ScrubIo(new StringReader(string.Empty), new Output(_out, _error, SliceHarness.Json), _error, Repo);
        Assert.Equal(0, await new ScrubVerb(io, Git, Redactor.Default, fake).Run(["--install-pre-commit"]));
        Stage("a.txt", "x\n");

        var pass = Git(Repo, ["commit", "-q", "-m", "one"]);
        Stage("b.txt", "y\n");
        var blocked = ProcessGit.Run(Repo, ["commit", "-q", "-m", "two"], new Dictionary<string, string?>(_gitEnvironment) { ["FAKE_EXIT"] = "1" });

        Assert.True(pass.Ok, pass.Stderr);
        Assert.False(blocked.Ok);
        Assert.Equal("ran\nran\n", File.ReadAllText(fake + ".log"));
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--staged", "--install-pre-commit")]
    [InlineData("--repo", "/x")]
    [InlineData("--install-pre-commit", "--repo")]
    [InlineData("stray")]
    public async Task Anything_else_is_a_usage_error(params string[] args)
    {
        var code = await Run(_temp.Path, string.Empty, args);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
    }
}
