using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The body of `POST /api/admin/api-key` (APIKeyArgs).</summary>
/// <param name="Name">The key's name.</param>
/// <param name="GroupIds">The groups the key's service account joins.</param>
internal sealed record KeyRequestWire(string Name, [property: JsonPropertyName("group_ids")] IReadOnlyList<int> GroupIds);
