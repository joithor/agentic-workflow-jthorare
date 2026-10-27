using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// The form check (sandbox spec §6): an agent a Step may run (installed, and approved for Steps) whose policy form can't carry a Step's rules is -015 ('form-none' or 'form-reserved').
/// An agent no Step may run is no blocker: nothing would park on it.
/// </summary>
/// <param name="setup">The catalog and what is installed.</param>
/// <param name="catalog">The entries' policy forms.</param>
internal sealed class SandboxFormCheck(ILaunchSetup setup, IAgentCatalog catalog) : ISandboxFormCheck
{
    private static readonly SandboxCode Unconfinable = SandboxCodes.All.First(static c => ReferenceEquals(c.Error, SindriErrors.LaunchUnconfinable));

    public async ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken)
    {
        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return [];
        }

        var blockers = new List<SandboxBlocker>();
        foreach (var target in shown.Value.Entries.Where(static e => e.ResolvedBin is not null && e.StepOk))
        {
            var reason = catalog.Resolve(target.Id)?.PolicyForm switch
            {
                PolicyForm.None => SandboxReasons.FormNone,
                PolicyForm.CursorConfig => SandboxReasons.FormReserved,
                _ => null,
            };
            if (reason is not null)
            {
                blockers.Add(new SandboxBlocker(Unconfinable, reason, Source: null, new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = target.Id.Value }));
            }
        }

        return blockers;
    }
}
