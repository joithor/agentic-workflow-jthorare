using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The profile's 'context.checks' as written: the mode and the limits of the map structure checks, every key optional, unknown keys refused.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksSection(string? Mode = null, ChecksIsolatedSection? Isolated = null, ChecksOverCapSection? OverCap = null);

/// <summary>'context.checks.isolated': which features are measured for isolation and the shares that trip it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksIsolatedSection(int? MinFiles = null, int? MinFeatures = null, int? DegradedPercent = null, int? RefusePercent = null);

/// <summary>'context.checks.overCap': the shares of features over the file cap that trip it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record ChecksOverCapSection(int? DegradedPercent = null, int? RefusePercent = null);
