using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.checks'. Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class ChecksRules
{
    /// <summary>The mode of a section: 'refuse' (default) or 'warn'.</summary>
    /// <param name="written">The mode as written, or null.</param>
    /// <returns>The mode, or null when it is neither.</returns>
    internal static ChecksMode? ModeOf(string? written) => written switch
    {
        null or "refuse" => ChecksMode.Refuse,
        "warn" => ChecksMode.Warn,
        _ => null,
    };

    /// <summary>The limits of a section; a missing section or key is its default.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The limits.</returns>
    internal static MapCheckLimits Limits(ChecksSection? section)
    {
        var d = new MapCheckLimits();
        return new MapCheckLimits
        {
            MinFilesIsolated = section?.Isolated?.MinFiles ?? d.MinFilesIsolated,
            MinFeaturesIsolated = section?.Isolated?.MinFeatures ?? d.MinFeaturesIsolated,
            IsolatedDegradedPercent = section?.Isolated?.DegradedPercent ?? d.IsolatedDegradedPercent,
            IsolatedRefusePercent = section?.Isolated?.RefusePercent ?? d.IsolatedRefusePercent,
            OverCapDegradedPercent = section?.OverCap?.DegradedPercent ?? d.OverCapDegradedPercent,
            OverCapRefusePercent = section?.OverCap?.RefusePercent ?? d.OverCapRefusePercent,
        };
    }

    /// <summary>The first value out of range, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Problem(ChecksSection? section)
    {
        if (section is null)
        {
            return null;
        }

        var l = Limits(section);
        return section switch
        {
            { Mode: { } mode } when ModeOf(mode) is null => SettingsReader.Invalid("context.checks.mode", "is 'refuse' (a map whose structure checks fail is not built) or 'warn' (it is built with a degraded note)"),
            { Isolated.MinFiles: < 1 or > 1_000 } => SettingsReader.Invalid("context.checks.isolated.minFiles", "is a whole number from 1 to 1000"),
            { Isolated.MinFeatures: < 1 or > 100_000 } => SettingsReader.Invalid("context.checks.isolated.minFeatures", "is a whole number from 1 to 100000"),
            { Isolated.DegradedPercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.isolated.degradedPercent", "is a whole percent from 0 to 100"),
            { Isolated.RefusePercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.isolated.refusePercent", "is a whole percent from 0 to 100"),
            { OverCap.DegradedPercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.overCap.degradedPercent", "is a whole percent from 0 to 100"),
            { OverCap.RefusePercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.overCap.refusePercent", "is a whole percent from 0 to 100"),
            _ when l.IsolatedDegradedPercent > l.IsolatedRefusePercent => SettingsReader.Invalid("context.checks.isolated.degradedPercent", "is at most refusePercent (the degraded limit comes first)"),
            _ when l.OverCapDegradedPercent > l.OverCapRefusePercent => SettingsReader.Invalid("context.checks.overCap.degradedPercent", "is at most refusePercent (the degraded limit comes first)"),
            _ => null,
        };
    }
}
