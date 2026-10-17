using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// Steps 7 and 8: the stored key is kept when it still works; otherwise the owner's login mints a new admin service-account key, which is verified, stored, and the login dropped.
[Lane(TestLane.Unit)]
public sealed class OnyxKeyStepTests
{
    private const string Stored = "on_stored-key-from-an-earlier-run";
    private const string Email = "owner@example.com";
    private const string Typed = "zebra-quartz-lantern-41";
    private const string EmailPrompt = "Onyx owner email: ";
    private const string HiddenPrompt = "Onyx owner password: ";

    private readonly List<string> _journal = [];
    private readonly FakeSecretPointers _pointers = new();
    private readonly FakeOwnerConsole _console = new();
    private readonly CapturingOnyxProgress _progress = new();
    private readonly OnyxUpRig _rig = new();
    private readonly FakeOnyxProvisioning _onyx;
    private readonly FakeSecretWriter _writer;

    public OnyxKeyStepTests()
    {
        _onyx = new FakeOnyxProvisioning { Calls = _journal, OwnerEmail = Email };
        _writer = new FakeSecretWriter { Journal = _journal, Into = _pointers };
        _onyx.OwnerPassword = Typed;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private OnyxKeyStep Step => new(_onyx, _writer, _pointers, _console, _progress);

    private OnyxUpContext Context => new(_rig.Pins.Pins, _rig.Paths, 3002, "linux/arm64");

    private async Task<(Result<OnyxStepOutcome> Outcome, OnyxUpContext Context)> Run()
    {
        var context = Context;
        var step = Step;
        return (await step.Execute(context, step.Stamp(context, "upstream"), recorded: null, upstreamRewrote: false, Token), context);
    }

    private void Typing() => _console.Feed(Email, Typed);

    [Fact]
    public async Task A_stored_key_that_answers_as_an_admin_is_kept_with_no_prompt_and_one_me_call()
    {
        _pointers.Values["keychain:onyx/sindri-onyx"] = Stored;
        _onyx.Accept(Stored, admin: true);

        var (outcome, context) = await Run();

        Assert.Equal(OnyxStepOutcome.Skipped, outcome.Value);
        Assert.Equal(["me:key"], _journal);
        Assert.Empty(_console.Prompts);
        Assert.Equal(OnyxKeyPointers.Keychain, context.KeyPointer);
    }

    [Fact]
    public async Task No_stored_key_prompts_logs_in_mints_verifies_logs_out_and_stores_in_that_order()
    {
        Typing();

        var (outcome, context) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal(["login", "mint", "me:key", "logout", "store"], _journal);
        Assert.Equal([EmailPrompt, HiddenPrompt], _console.Prompts);
        Assert.Equal(["sindri-onyx"], _onyx.MintedNames);
        Assert.Equal([_onyx.MintedKey], _writer.Stored);
        Assert.Equal(OnyxKeyPointers.Keychain, context.KeyPointer);
    }

    [Fact]
    public async Task A_stored_key_that_returns_401_mints_a_new_one()
    {
        _pointers.Values["keychain:onyx/sindri-onyx"] = Stored;   // Onyx doesn't know it (a reset Onyx)
        Typing();

        var (outcome, _) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal("me:key", _journal[0]);
        Assert.Contains("mint", _journal, StringComparer.Ordinal);
        Assert.Contains(_progress.Lines, static l => l.Contains("isn't accepted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_unreadable_or_locked_keychain_counts_as_no_stored_key_and_says_so()
    {
        Typing();   // nothing resolves: the keychain can't be read and there is no fallback file

        var (outcome, _) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Contains(_progress.Lines, static l => l.Contains("no stored key", StringComparison.Ordinal) && l.Contains("creating", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_stored_key_in_the_fallback_file_is_kept_when_the_keychain_cannot_be_read()
    {
        var file = OnyxKeyPointers.FileFor(_rig.Paths.SindriHome);
        _pointers.Values[file.ToString()] = Stored;
        _onyx.Accept(Stored, admin: true);

        var (outcome, context) = await Run();

        Assert.Equal(OnyxStepOutcome.Skipped, outcome.Value);
        Assert.Equal(file, context.KeyPointer);
        Assert.Equal(["me:key"], _journal);
    }

    [Fact]
    public async Task A_non_admin_login_is_007_and_stores_nothing_and_still_logs_out()
    {
        _onyx.OwnerIsAdmin = false;
        Typing();

        var (outcome, _) = await Run();

        Assert.Equal("SND-ONYX-007", outcome.Error.Code);
        Assert.Empty(_writer.Stored);
        Assert.Equal(["login", "mint", "logout"], _journal);
    }

    [Fact]
    public async Task A_wrong_password_is_008_and_never_echoes_it()
    {
        const string wrong = "a-wrong-phrase-5521";
        _console.Feed(Email, wrong);

        var (outcome, _) = await Run();

        Assert.Equal("SND-ONYX-008", outcome.Error.Code);
        Assert.DoesNotContain(wrong, outcome.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(wrong, _console.Terminal, StringComparison.Ordinal);
        Assert.DoesNotContain(_progress.Lines, l => l.Contains(wrong, StringComparison.Ordinal));
        Assert.DoesNotContain("mint", _journal, StringComparer.Ordinal);
        Assert.Empty(_writer.Stored);
    }

    [Fact]
    public async Task A_failed_verification_is_008_and_stores_nothing()
    {
        _onyx.MintedKeyIsAdmin = false;
        Typing();

        var (outcome, _) = await Run();

        Assert.Equal("SND-ONYX-008", outcome.Error.Code);
        Assert.Contains("id 41", outcome.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_writer.Stored);
        Assert.Contains("logout", _journal, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_failed_store_is_009_naming_the_orphan_key_id()
    {
        _writer.Error = SindriErrors.OnyxSecretWriteFailed.With("the keychain refused");
        Typing();

        var (outcome, context) = await Run();

        Assert.Equal("SND-ONYX-009", outcome.Error.Code);
        Assert.Contains("sindri-onyx", outcome.Error.Message, StringComparison.Ordinal);
        Assert.Contains("id 41", outcome.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_onyx.MintedKey, outcome.Error.Message, StringComparison.Ordinal);
        Assert.Null(context.KeyPointer);
    }

    [Fact]
    public async Task The_raw_key_and_password_are_absent_from_every_progress_line_and_error()
    {
        Typing();
        var good = await Run();
        var failing = new OnyxKeyStepTests();
        failing._writer.Error = SindriErrors.OnyxSecretWriteFailed.With("no store");
        failing.Typing();
        var bad = await failing.Run();
        var refusing = new OnyxKeyStepTests();
        refusing._onyx.OwnerIsAdmin = false;
        refusing.Typing();
        var refused = await refusing.Run();
        var text = string.Join('\n', [.. _progress.Lines, _console.Terminal, .. failing._progress.Lines, failing._console.Terminal, bad.Outcome.Error.Message, refused.Outcome.Error.Message]);

        Assert.True(good.Outcome.IsSuccess);
        Assert.DoesNotContain(_onyx.MintedKey, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Typed, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Stored, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_interactive_returns_NeedsHuman_when_a_mint_is_needed_but_not_when_the_stored_key_works()
    {
        _console.Interactive = false;
        var (needs, context) = await Run();
        _pointers.Values["keychain:onyx/sindri-onyx"] = Stored;
        _onyx.Accept(Stored, admin: true);

        var (kept, _) = await Run();

        Assert.Equal(OnyxStepOutcome.NeedsHuman, needs.Value);
        Assert.Contains("terminal", context.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("login", _journal, StringComparer.Ordinal);
        Assert.Equal(OnyxStepOutcome.Skipped, kept.Value);
    }

    [Fact]
    public async Task The_pointer_returned_by_the_writer_is_what_the_context_carries_to_the_profile_step()
    {
        var file = OnyxKeyPointers.FileFor(_rig.Paths.SindriHome);
        _writer.Answer = file;
        Typing();

        var (outcome, context) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal(file, context.KeyPointer);
    }

    [Fact]
    public async Task A_failed_logout_is_a_warning_not_an_error()
    {
        _onyx.LogoutError = SindriErrors.OnyxKeyFailed.With("Onyx's logout answered 500");
        Typing();

        var (outcome, _) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Contains(_progress.Lines, static l => l.Contains("log out", StringComparison.Ordinal));
        Assert.Equal([_onyx.MintedKey], _writer.Stored);
    }

    [Fact]
    public async Task End_of_input_at_the_email_or_the_password_is_NeedsHuman_with_no_login()
    {
        _console.Feed([null]);
        var (noEmail, _) = await Run();
        _console.Feed(Email, null);

        var (noPassword, _) = await Run();

        Assert.Equal(OnyxStepOutcome.NeedsHuman, noEmail.Value);
        Assert.Equal(OnyxStepOutcome.NeedsHuman, noPassword.Value);
        Assert.DoesNotContain("login", _journal, StringComparer.Ordinal);
    }

    [Fact]
    public void The_stamp_chains_the_upstream_and_names_the_key()
    {
        var step = Step;

        Assert.NotEqual(step.Stamp(Context, "a"), step.Stamp(Context, "b"), StringComparer.Ordinal);
        Assert.Equal("key", step.Name);
    }
}
