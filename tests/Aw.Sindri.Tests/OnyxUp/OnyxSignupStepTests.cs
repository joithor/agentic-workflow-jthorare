using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// Step 6: the owner signs up in the browser; Sindri sends no registration and asks for nothing else.
[Lane(TestLane.Unit)]
public sealed class OnyxSignupStepTests
{
    private const string Prompt = "Create your Onyx account in the browser, then press Enter";
    private const string Hint = "run `sindri onyx up` in a terminal; it opens Onyx's sign-up page and waits for you";

    private readonly OnyxUpRig _rig = new();
    private readonly FakeOwnerConsole _console = new();
    private readonly FakeBrowser _browser = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private OnyxSignupStep Step => new(_console, _browser, _rig.Progress);

    private OnyxUpContext Context => new(_rig.Pins.Pins, _rig.Paths, 3002, "linux/arm64");

    private async Task<(Result<OnyxStepOutcome> Outcome, OnyxUpContext Context)> Run(string? recorded = null)
    {
        var context = Context;
        var step = Step;
        return (await step.Execute(context, step.Stamp(context, "upstream"), recorded, upstreamRewrote: false, Token), context);
    }

    [Fact]
    public async Task Signup_opens_the_sign_up_page_and_waits_then_runs()
    {
        var (outcome, _) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Equal(["http://127.0.0.1:3002/auth/create-account"], _browser.Opened.Select(static u => u.AbsoluteUri), StringComparer.Ordinal);
        Assert.Equal([Prompt], _console.Prompts);
        Assert.Contains(_rig.Progress.Lines, static l => l.Contains("http://127.0.0.1:3002/auth/create-account", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Signup_in_a_non_interactive_run_returns_NeedsHuman_with_the_hint_and_opens_nothing()
    {
        _console.Interactive = false;

        var (outcome, context) = await Run();

        Assert.Equal(OnyxStepOutcome.NeedsHuman, outcome.Value);
        Assert.Equal(Hint, context.Hint);
        Assert.Empty(_browser.Opened);
        Assert.Empty(_console.Prompts);
    }

    [Fact]
    public async Task Signup_on_a_rerun_with_the_recorded_stamp_is_skipped_without_prompting()
    {
        var step = Step;
        var stamp = step.Stamp(Context, "upstream");

        var outcome = await step.Execute(Context, stamp, recorded: stamp, upstreamRewrote: false, Token);

        Assert.Equal(OnyxStepOutcome.Skipped, outcome.Value);
        Assert.Empty(_console.Prompts);
        Assert.Empty(_browser.Opened);
    }

    [Fact]
    public void Signup_sends_no_http_request()
    {
        var parameters = typeof(OnyxSignupStep).GetConstructors().Single().GetParameters().Select(static p => p.ParameterType).ToList();

        Assert.DoesNotContain(typeof(IOnyxProvisioning), parameters);
        Assert.DoesNotContain(typeof(IOnyxHealth), parameters);
        Assert.DoesNotContain(typeof(HttpClient), parameters);
    }

    [Fact]
    public async Task EOF_while_waiting_is_NeedsHuman_and_records_nothing()
    {
        _rig.Console.Feed([null]);

        var response = (await _rig.Up(Token)).Value;

        Assert.Equal(OnyxStepOutcome.NeedsHuman, response.Steps[^1].Outcome);
        Assert.Equal("signup", response.Steps[^1].Step);
        Assert.False(_rig.Workspace.State!.Steps.ContainsKey("signup"));
        Assert.DoesNotContain(response.Steps, static s => string.Equals(s.Step, "key", StringComparison.Ordinal) || string.Equals(s.Step, "profile", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_browser_that_cannot_open_still_prints_the_address_and_waits()
    {
        _browser.Result = false;

        var (outcome, _) = await Run();

        Assert.Equal(OnyxStepOutcome.Ran, outcome.Value);
        Assert.Contains(_rig.Progress.Lines, static l => l.Contains("http://127.0.0.1:3002/auth/create-account", StringComparison.Ordinal) && l.Contains("open it", StringComparison.Ordinal));
        Assert.Equal([Prompt], _console.Prompts);
    }
}
