using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 6: the owner creates the Onyx account in the browser. Sindri sends no registration (spec O3) and makes no HTTP request: it opens the page and waits for Enter.
// A rerun with the step recorded is skipped without a prompt (D5: no network, no user-count route); no terminal, end of input and Ctrl-C leave it unrecorded.
internal sealed class OnyxSignupStep(IOwnerConsole console, IBrowser browser, IOnyxProgress progress) : IOnyxUpStep
{
    private const string Prompt = "Create your Onyx account in the browser, then press Enter";
    private const string NeedsTerminal = "run `sindri onyx up` in a terminal; it opens Onyx's sign-up page and waits for you";

    public string Name => OnyxSteps.Signup;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<OnyxStepOutcome>>(Run(context, stamp, recorded));

    private OnyxStepOutcome Run(OnyxUpContext context, string stamp, string? recorded)
    {
        if (string.Equals(recorded, stamp, StringComparison.Ordinal))
        {
            return OnyxStepOutcome.Skipped;
        }

        if (!console.Interactive)
        {
            return Waiting(context);
        }

        var page = OnyxPages.SignUp(context.Port);
        progress.Line(browser.Open(page) ? $"{Name}: opening {page}" : $"{Name}: couldn't open a browser; open it yourself: {page}");
        return console.WaitForEnter(Prompt) ? OnyxStepOutcome.Ran : Waiting(context);
    }

    private static OnyxStepOutcome Waiting(OnyxUpContext context)
    {
        context.Hint = NeedsTerminal;
        return OnyxStepOutcome.NeedsHuman;
    }
}
