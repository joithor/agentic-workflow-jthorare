using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 3: the .env and the compose override (stamped by the fetched files, port, platform, release and this platform's image digests). A present auth key is always kept;
// a missing one is generated once. Both files are rendered on every run and written only when they differ, so an edited or deleted file is put back.
internal sealed class OnyxConfigStep(IOnyxWorkspace workspace, IComposeServices services, IRandomHex random, IOnyxProgress progress) : IOnyxUpStep
{
    private const int AuthKeyBytes = 32;

    public string Name => OnyxSteps.Config;

    public string Stamp(OnyxUpContext context, string? upstream)
        => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream), context.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), context.Platform, context.Pins.Version, ImageRows(context));

    // The override names every image by its digest, so the digests this platform runs are inputs of the step (a moved pin re-renders, re-pulls and restarts).
    private static string ImageRows(OnyxUpContext context)
        => string.Join('\n', context.Pins.Images.Where(i => i.Platform == context.Platform).Select(static i => i.Repository + "@" + i.Digest).Order(StringComparer.Ordinal));

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, bool upstreamRewrote, CancellationToken cancellationToken)
    {
        var files = ReadFiles(context.Paths);
        return Done(files.IsError ? files.Error : Render(context, stamp, recorded, files.Value));
    }

    // What is on disk now; a file that exists but can't be read is SND-ONYX-012, not a missing one.
    private Result<Files> ReadFiles(OnyxPaths paths)
    {
        var reads = new[] { workspace.Read(paths.Compose), workspace.Read(paths.EnvTemplate), workspace.Read(paths.Env), workspace.Read(paths.Override) };
        return reads.FirstOrDefault(static r => r.IsError) is { } failed ? failed.Error : new Files(reads[0].Value, reads[1].Value, reads[2].Value, reads[3].Value);
    }

    private Result<OnyxStepOutcome> Render(OnyxUpContext context, string stamp, string? recorded, Files files)
    {
        if (files.Compose is null || files.Template is null)
        {
            return SindriErrors.OnyxPinsMalformed.With("the fetched compose file or env.template is missing; run `sindri onyx up` again to fetch them");
        }

        var key = OnyxConfig.ReadSecret(files.Env) ?? random.Hex(AuthKeyBytes);
        var settings = context.Settings(key);
        var rendered = services.Read(files.Compose).Bind(list => OnyxConfig.RenderOverride(list, settings, context.Pins, context.Platform));
        if (rendered.IsError)
        {
            return rendered.Error;
        }

        var env = OnyxConfig.RenderEnv(files.Template, settings);
        var stale = IsStale(recorded, stamp, files, env, rendered.Value);
        if (stale)
        {
            progress.Line($"{Name}: running");
        }

        var wrote = Write(context.Paths, env, rendered.Value);
        return Outcome(stale, wrote, inUse: recorded is not null || files.Env is not null || files.Override is not null);
    }

    // A write over a config that was in use (a recorded step, or a file that was there) is a rewrite: a running stack still holds the old one.
    private static OnyxStepOutcome Outcome(bool stale, bool wrote, bool inUse)
        => wrote && inUse ? OnyxStepOutcome.Rewrote : stale || wrote ? OnyxStepOutcome.Ran : OnyxStepOutcome.Skipped;

    private static bool IsStale(string? recorded, string stamp, Files files, string env, string over)
        => !string.Equals(recorded, stamp, StringComparison.Ordinal)
            || !string.Equals(files.Env, env, StringComparison.Ordinal)
            || !string.Equals(files.Override, over, StringComparison.Ordinal);

    // Both files are written when they differ.
    private bool Write(OnyxPaths paths, string env, string over)
    {
        var wroteEnv = workspace.WriteIfChanged(paths.Env, env, secret: true);
        var wroteOver = workspace.WriteIfChanged(paths.Override, over, secret: false);
        return wroteEnv || wroteOver;
    }

    private static ValueTask<Result<OnyxStepOutcome>> Done(Result<OnyxStepOutcome> result) => ValueTask.FromResult(result);

    private sealed record Files(string? Compose, string? Template, string? Env, string? Override);
}
