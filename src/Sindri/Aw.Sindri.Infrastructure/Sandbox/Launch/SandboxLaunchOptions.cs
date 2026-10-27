namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>What the sandbox step reads besides the plan: the systemd-run program, the box's size and the longest wall any Step gets.</summary>
/// <param name="SystemdRun">The systemd-run program (AW_SANDBOX_SYSTEMD_RUN or <c lang="text">systemd-run</c>).</param>
/// <param name="Box">The box's RAM and cores (TODO(switch builder): read from /proc/meminfo and the processor count).</param>
/// <param name="LongMax">'workflow.steps.longMax' as a duration; the Step's own timeout is never longer.</param>
internal sealed record SandboxLaunchOptions(string SystemdRun, BoxFacts Box, TimeSpan LongMax);
