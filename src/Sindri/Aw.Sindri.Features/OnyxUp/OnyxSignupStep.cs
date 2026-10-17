using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 6: the owner creates the Onyx account in the browser. Sindri sends no registration (spec O3).
internal sealed class OnyxSignupStep(IOwnerConsole console, IBrowser browser, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Signup;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
    {
        _ = (console, browser, progress);
        return ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Skipped);
    }
}
