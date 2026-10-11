using System.Text.Json;
using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Builtin;
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
    /// built-in blocks and the block registry, and the workflow engine. Another project adds its blocks as IBlock
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
            _ = services.AddSingleton<IBlock>(block);
        }

        return services
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
}
