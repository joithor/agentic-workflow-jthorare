using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ShowWorkflow;

/// <summary>Show one instance: its row, its state's next step, its children and its event history.</summary>
/// <param name="Instance">The instance.</param>
public sealed record ShowWorkflowRequest(InstanceId Instance);
