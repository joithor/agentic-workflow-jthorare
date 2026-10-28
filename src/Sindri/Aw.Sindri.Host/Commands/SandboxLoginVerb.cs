namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri sandbox login &lt;claude-code|codex&gt;' (sandbox spec SB-8, §3.4): signs the target in to its own agent home, the login Steps use when 'sandbox.login.source' is separate-login.
/// Human-only and not an API route: a session Sindri launched, or a CLI with no readable per-start token, is refused (SND-AUTH-002). Exit code: the sign-in's; 2 for usage or a refusal.
/// </summary>
/// <param name="output">Where the verb writes.</param>
/// <param name="services">The container: the login port.</param>
/// <param name="caller">Tells a human from a session.</param>
internal sealed class SandboxLoginVerb(Output output, IServiceProvider services, CallerHeaders caller)
{
    internal const string Usage = "usage: sindri sandbox login <claude-code|codex>";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        _ = (output, services, await caller.Resolve(ct).ConfigureAwait(false));   // red: the real verb lands with the next commit
        throw new NotSupportedException();
    }
}
