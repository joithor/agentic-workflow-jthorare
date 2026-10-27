using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>One instance parked on a sandbox code.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Since">When it parked.</param>
public sealed record ParkedOnSandbox(WorkflowInstanceId Instance, DateTimeOffset Since);
