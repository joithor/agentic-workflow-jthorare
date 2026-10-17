using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 6: a shell that does nothing yet, so the plan's order holds; its task gives it the real work.
internal sealed class OnyxSignupStep : IOnyxUpStep
{
    public string Name => OnyxSteps.Signup;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Skipped);
}
