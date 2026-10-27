namespace Aw.Sindri.Core.Sandbox;

/// <summary>One reason this box can't confine a Step right now, as the Check again card lists it (sandbox spec §6, §8).</summary>
/// <param name="Code">The SND-LAUNCH code (-014 to -017).</param>
/// <param name="Reason">The code's reason (<see cref="SandboxReasons"/>), which picks its §8 sentence.</param>
/// <param name="Source">What found it: the probe, the form check, a login source or the self-check; null when the code says enough.</param>
public sealed record SandboxBlocker(SandboxCode Code, string Reason, string? Source = null);
