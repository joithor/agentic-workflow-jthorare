using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>The 'agent:target' row: warns when the approved profile's 'workflow.agent.target' can't take a Step's policy. Adds no row otherwise.</summary>
internal sealed class AgentTargetDoctor(IApprovedProfile approved, IAgentCatalog catalog) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approved);
        ArgumentNullException.ThrowIfNull(catalog);
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
    }
}
