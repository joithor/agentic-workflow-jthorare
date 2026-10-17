using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxUp;

// Step 9: write context.sources.onyx into the profile with the managed entry: this run's port, the pointer the key step made sure of (the keychain's or the file fallback's) and the
// pinned release. The writer keeps every other key and comment and never replaces a hand-written entry; nothing here touches approval (step 10 asks for it).
internal sealed class OnyxProfileStep(IProfileWriter writer, IOnyxPinsSource pins, ISecretPointers pointers, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Profile;

    public string Stamp(OnyxUpContext context, string? upstream)
        => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), context.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), context.KeyPointer?.ToString() ?? string.Empty, pins.Pins.Version);

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
        => ValueTask.FromResult(Write(context));

    private Result<OnyxStepOutcome> Write(OnyxUpContext context)
    {
        var pointer = context.KeyPointer ?? StoredPointer(context);
        if (pointer is null)
        {
            return SindriErrors.OnyxKeyFailed.With($"there is no stored Onyx key to name in the profile; {OnyxFixes.KeyFailed}");
        }

        var written = writer.SetOnyxSource(context.Port, pointer, pins.Pins.Version);
        if (written.IsError)
        {
            return written.Error;
        }

        if (written.Value == ProfileWriteOutcome.Unchanged)
        {
            return OnyxStepOutcome.Skipped;
        }

        progress.Line($"{Name}: wrote context.sources.onyx ({pointer}) into the profile; it needs approving");
        return OnyxStepOutcome.Ran;
    }

    // The key step normally hands the pointer over; when it didn't, the first stored one that resolves is the one the profile names.
    private SecretPointer? StoredPointer(OnyxUpContext context)
        => new[] { OnyxKeyPointers.Keychain, OnyxKeyPointers.FileFor(context.Paths.SindriHome) }.FirstOrDefault(p => pointers.Resolve(p).IsSuccess);
}
