using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxUp;

// Steps 7 and 8: mint Sindri's admin service-account key from the owner's login and store it. A shell until its task gives it the work.
internal sealed class OnyxKeyStep(IOnyxProvisioning provisioning, ISecretWriter writer, ISecretPointers pointers, IOwnerConsole console, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Key;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), Name, OnyxKeyPointers.Account);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
    {
        _ = (provisioning, writer, pointers, console, progress);
        return ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Skipped);
    }
}
