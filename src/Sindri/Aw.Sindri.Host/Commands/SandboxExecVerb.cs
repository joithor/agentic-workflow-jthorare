namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri sandbox exec &lt;launch|run-area&gt; [--json] [-- &lt;command...&gt;]' (sandbox spec §3.6): a human-only CLI verb; the stub of the red commit.</summary>
internal sealed class SandboxExecVerb
{
    internal const string Usage = "usage: sindri sandbox exec <launch|run-area> [--json] [-- <command...>]";

    internal Task<int> Run(string[] args, CancellationToken ct) => throw new NotSupportedException();
}
