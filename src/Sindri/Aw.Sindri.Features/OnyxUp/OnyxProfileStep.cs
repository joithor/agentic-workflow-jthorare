using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxUp;

// Step 9: write context.sources.onyx into the profile. A shell until its task gives it the work. It never touches approval.
internal sealed class OnyxProfileStep(IProfileWriter writer, IOnyxPinsSource pins, ISecretPointers pointers, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Profile;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
    {
        _ = (writer, pins, pointers, progress);
        return ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Skipped);
    }
}
