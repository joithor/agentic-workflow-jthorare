using System.Text.Json;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Builtin;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Registration;

/// <summary>Registers Aw.Sindri.Core's types.</summary>
public static class SindriCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Sindri domain services: the control plane's event scope and factory, the lease this process holds, the
    /// built-in blocks, the tool blocks (judge and ui-evidence, docs/csharp/sindri-blocks.md) and the block registry, and the
    /// workflow engine. Another project adds its blocks as IBlock
    /// singletons in its own Add…() method; the registry collects every IBlock in the container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriCore(this IServiceCollection services)
    {
        _ = services
            .AddSingleton(EventScope.ControlPlane)
            .AddSingleton<EventFactory>()
            .AddSingleton<HeldLease>();
        foreach (var block in BuiltinSteps.All.Concat(BuiltinGates.All))
        {
            _ = services.AddSingleton(block);
        }

        return AddToolBlocks(services)
            .AddSingleton<IBlock>(static sp => new NotifyHumanEffect(sp.GetRequiredService<JsonSerializerOptions>()))
            .AddSingleton<IBlock>(static sp => new DraftPullRequestEffect(sp.GetRequiredService<JsonSerializerOptions>()))
            .AddSingleton<IBlockRegistry, BlockRegistry>()
            .AddScoped<InstanceLoader>()
            .AddSingleton<GateEvaluator>()
            .AddScoped<CommitBuilder>()
            .AddScoped<FollowUps>()
            .AddScoped<ParentDecisions>()
            .AddScoped<ParentPropagation>()
            .AddScoped<Committer>()
            .AddScoped<SignalApplier>()
            .AddScoped<CommandApplier>()
            .AddScoped<WorkflowTicker>()
            .AddScoped<IWorkflowEngine, WorkflowRunner>();
    }

    // The tool blocks (docs/csharp/sindri-blocks.md): judge's decision questions as gates, ui-evidence's checks and
    // providers. They reach their tools through IJudgeClient and IUiEvidence, which an Infrastructure Add…() registers.
    private static IServiceCollection AddToolBlocks(IServiceCollection services)
    {
        foreach (var question in JudgeQuestions.All)
        {
            _ = services.AddSingleton<IBlock>(sp => new JudgeGate(question, sp.GetRequiredService<IJudgeClient>()));
        }

        foreach (var make in new Func<IUiEvidence, UiRunProvider>[] { UiRunProvider.UiBrowser, UiRunProvider.DesignParity })
        {
            _ = services.AddSingleton<IBlock>(sp => make(sp.GetRequiredService<IUiEvidence>()));
        }

        return services
            .AddSingleton<IBlock>(static sp => new StackDoctorGate(sp.GetRequiredService<IUiEvidence>()))
            .AddSingleton<IBlock>(static sp => new ProvenanceGate(sp.GetRequiredService<IUiEvidence>()))
            .AddSingleton(UiEvidenceSteps.UiRun)
            .AddSingleton<EvidenceProviders>();
    }
}
