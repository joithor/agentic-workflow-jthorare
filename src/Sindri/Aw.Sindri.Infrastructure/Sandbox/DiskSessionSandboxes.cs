using System.Text.Json;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>ISessionSandboxes over a launch's stored plan, '&lt;home&gt;/launches/&lt;id&gt;.sandbox.json', and its cgroup scope.</summary>
/// <param name="settings">Sindri's home.</param>
/// <param name="options">The package's AwJson options.</param>
/// <param name="stats">The cgroup reader.</param>
/// <param name="sliceCgroupPath">The slice's cgroup path under the cgroup root, with no leading slash.</param>
internal sealed class DiskSessionSandboxes(SindriSettings settings, JsonSerializerOptions options, ICgroupStats stats, string sliceCgroupPath) : ISessionSandboxes
{
    public ValueTask<SessionSandbox> Read(LaunchId launch, SandboxSummary summary, CancellationToken cancellationToken)
    {
        _ = (settings, options, stats, sliceCgroupPath);   // red: the reader arrives with the next commit
        throw new NotSupportedException();
    }
}
