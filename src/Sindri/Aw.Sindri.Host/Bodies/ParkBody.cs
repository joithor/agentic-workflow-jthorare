using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Host.Bodies;

/// <summary>The body of POST …/park.</summary>
internal sealed record ParkBody(ParkReason Reason, string? Detail = null);
