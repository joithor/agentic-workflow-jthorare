using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentStep' is given (engine P8 U3): the run as the planner reads it, and the stored 'fact:item' that names the item and its repo.</summary>
/// <param name="Run">The run: the instance, the run token, the Step and whether it may write.</param>
/// <param name="Item">The instance's stored 'fact:item'; null when it has none.</param>
/// <param name="Workspace">The instance's stored 'fact:workspace' when the Step reads it: the session works in that worktree instead of a new one; null when the Step reads none or the instance holds none.</param>
internal sealed record AgentLaunchRun(AgentStepRun Run, FactValue? Item, FactValue? Workspace = null);
