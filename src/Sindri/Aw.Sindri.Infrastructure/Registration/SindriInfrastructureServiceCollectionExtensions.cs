using Aw.Kernel.Ids;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Infrastructure.Tools;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Registration;

/// <summary>Registers Aw.Sindri.Infrastructure's adapters and settings. A second technology for a port gets its own AddSindri&lt;Tech&gt;Infrastructure method.</summary>
public static class SindriInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Sindri adapters (SQLite ledger, lease and workflow store, the recipe catalog, YAML profile, files, the process probe, the judge and ui-evidence CLIs the tool blocks run) and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>The profile and approval adapters take the package's AwJson options, which Compose registers.</remarks>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<IIdGenerator, GuidV7Ids>()
            .AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>()
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<IWorkflowStore, SqliteWorkflowStore>()
            .AddSingleton<IRecipeCatalog, FileRecipeCatalog>()
            .AddSingleton<IProfileSource, YamlProfileSource>()
            .AddSingleton<IApprovals, FileApprovals>()
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton(ToolSettings.Read(environment))
            .AddSingleton<IJudgeClient, ProcessJudgeClient>()
            .AddSingleton<IUiEvidence, ProcessUiEvidence>();
}
