using Aw.Sindri.Core.Blocks;
using Aw.Sindri.Core.Blocks.Evidence;
using Aw.Sindri.Core.Blocks.Judge;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Registration;

/// <summary>Registers the tool blocks (factory addendum §17.28.2, "Tools versus workflows"): judge's decision questions and ui-evidence's checks and providers.</summary>
public static class SindriToolBlocksServiceCollectionExtensions
{
    /// <summary>
    /// Adds a 'gate.judge.&lt;question&gt;' gate per judge decision question, the 'gate.evidence.doctor' and
    /// 'gate.evidence.provenance' gates, the 'evidence.ui-browser' and 'evidence.design-parity' providers (looked up through
    /// EvidenceProviders) and the 'step.ui-run' step. They reach their tools through IJudgeClient and
    /// IUiEvidence, which an Infrastructure Add…() registers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriToolBlocks(this IServiceCollection services)
    {
        foreach (var question in JudgeQuestions.All)
        {
            _ = services.AddSingleton<IBlock>(sp => new JudgeGate(question, sp.GetRequiredService<IJudgeClient>()));
        }

        _ = services
            .AddSingleton<IBlock>(static sp => new StackDoctorGate(sp.GetRequiredService<IUiEvidence>()))
            .AddSingleton<IBlock>(static sp => new ProvenanceGate(sp.GetRequiredService<IUiEvidence>()))
            .AddSingleton(UiEvidenceSteps.UiRun);
        foreach (var make in new Func<IUiEvidence, UiRunProvider>[] { UiRunProvider.UiBrowser, UiRunProvider.DesignParity })
        {
            _ = services.AddSingleton<IBlock>(sp => make(sp.GetRequiredService<IUiEvidence>()));
        }

        return services.AddSingleton<EvidenceProviders>();
    }
}
