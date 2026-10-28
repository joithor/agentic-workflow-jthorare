using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>
/// The login probe (sandbox spec §3.4, §6): for every agent a Step may run and whose policy form is expressible, the credential source the profile picks (a repo's, else the
/// agent's, else the profile's) must deliver the model login. Never tries another source. Distinct failures are listed once.
/// </summary>
/// <param name="setup">The catalog and what is installed.</param>
/// <param name="catalog">The entries' policy forms.</param>
/// <param name="approved">The approved profile.</param>
/// <param name="sources">The credential sources.</param>
internal sealed class SandboxLoginCheck(ILaunchSetup setup, IAgentCatalog catalog, IApprovedProfile approved, ModelCredentialSources sources) : ISandboxLoginCheck
{
    /// <summary>A floor for how long the login must stay valid: a Step's own wall time is only known at launch.</summary>
    private static readonly TimeSpan _needed = TimeSpan.FromMinutes(30);

    public async ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken)
    {
        var shown = await setup.Show(only: null, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return [];
        }

        var profile = await approved.Latest(cancellationToken).ConfigureAwait(false);
        var settings = profile.IsSuccess ? profile.Value?.Profile.Sandbox : null;
        var repos = RepoNames(settings);
        var found = new Dictionary<(string Target, string Source, string Reason), SandboxBlocker>();
        foreach (var target in shown.Value.Entries.Where(e => e.ResolvedBin is not null && e.StepOk && Expressible(e.Id)))
        {
            foreach (var blocker in repos.Select(repo => Probe(settings, target.Id, repo)).OfType<SandboxBlocker>())
            {
                _ = found.TryAdd((target.Id.Value, blocker.Source ?? string.Empty, blocker.Reason), blocker);
            }
        }

        return [.. found.Values];
    }

    /// <summary>A name for 'no repo in particular', then every repo the profile's sandbox section names (a repo may pick its own source).</summary>
    private static List<RepoName> RepoNames(SandboxSettings? settings)
    {
        var repos = new List<RepoName> { RepoName.From("sandbox-check") };
        repos.AddRange((settings?.Repos?.Keys ?? []).Where(static k => RepoName.TryParse(k, provider: null, out _)).Select(RepoName.From));
        return repos;
    }

    private SandboxBlocker? Probe(SandboxSettings? settings, AgentId target, RepoName repo)
    {
        var source = LoginSourceResolver.Resolve(settings, target, repo, stepAsk: null);
        if (sources.Check(source, target, _needed) is not { IsError: true } failed)
        {
            return null;
        }

        var blocker = SandboxBlocker.Of(failed.Error);
        var values = new Dictionary<string, string>(blocker.Values ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal) { ["target"] = target.Value };
        return blocker with { Values = values };
    }

    private bool Expressible(AgentId id) => catalog.Resolve(id)?.PolicyForm is PolicyForm.ClaudeSettingsFile or PolicyForm.CodexOverrides;
}
