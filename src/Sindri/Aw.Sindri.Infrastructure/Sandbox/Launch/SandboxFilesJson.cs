using System.Text.Json;
using System.Text.Json.Serialization;
using Aw.Kernel.Json;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The AwJson options for the files the launcher writes for a session: the plan file and control.json. Same wire as the host's context, which reads them.</summary>
internal static class SandboxFilesJson
{
    internal static readonly JsonSerializerOptions Options = AwJson.CreateOptions(SandboxFilesJsonContext.Default);
}

/// <summary>The source-generated context behind <see cref="SandboxFilesJson"/>.</summary>
[JsonSerializable(typeof(Core.Sandbox.Plan.SandboxPlan))]
[JsonSerializable(typeof(Core.Control.ControlEndpoint))]
internal sealed partial class SandboxFilesJsonContext : JsonSerializerContext;
