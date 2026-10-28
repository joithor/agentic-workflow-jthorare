using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The source-generated context behind <see cref="SandboxFilesJson"/>.</summary>
[JsonSerializable(typeof(Core.Sandbox.Plan.SandboxPlan))]
[JsonSerializable(typeof(Core.Control.ControlEndpoint))]
internal sealed partial class SandboxFilesJsonContext : JsonSerializerContext;
