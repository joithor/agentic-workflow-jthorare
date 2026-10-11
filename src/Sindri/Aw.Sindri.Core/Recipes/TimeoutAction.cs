using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Recipes;

/// <summary>What a state's timeout does.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimeoutAction>))]
public enum TimeoutAction
{
    /// <summary>Park the instance with the reason 'timeout'.</summary>
    [JsonStringEnumMemberName("park")]
    Park = 1,

    /// <summary>Escalate the instance to the human.</summary>
    [JsonStringEnumMemberName("escalate")]
    Escalate = 2,

    /// <summary>Restart the state's timer once; a second timeout parks.</summary>
    [JsonStringEnumMemberName("retry")]
    Retry = 3,
}
