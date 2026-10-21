using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Pr;

/// <summary>Registers the CI-red autofix loop's runtimes: the snapshot and ci-log Steps, the three gates, the push and rerun effects and the ci-fix Step's instructions. Their descriptors are registered with the blocks.</summary>
internal static class PrServiceCollectionExtensions
{
    /// <summary>Adds the pull request slices.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPrSlices(this IServiceCollection services) => services
        .AddBlockRuntime<PrSnapshotStep>()
        .AddBlockRuntime<PrCiLogStep>()
        .AddGate<DiffScopeGate>()
        .AddGate<SecretScanGate>()
        .AddGate<AutofixCapGate>()
        .AddEffect<PushEffect>()
        .AddEffect<RerunChecksEffect>()
        .AddSingleton<IAgentStepInstructions, CiFixInstructions>();
}
