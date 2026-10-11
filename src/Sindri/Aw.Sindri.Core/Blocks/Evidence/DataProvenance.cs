using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks.Evidence;

/// <summary>Whether the local database holds only seed data.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataProvenance>))]
public enum DataProvenance
{
    /// <summary>Only seed data: evidence may leave the machine.</summary>
    [JsonStringEnumMemberName("seeded")]
    Seeded = 1,

    /// <summary>Not proven seed data: evidence stays local.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown = 2,
}
