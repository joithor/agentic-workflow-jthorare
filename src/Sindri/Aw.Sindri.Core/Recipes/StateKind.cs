using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A recipe state's kind (factory addendum §17.28.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateKind>))]
public enum StateKind
{
    /// <summary>A Step or session is working.</summary>
    [JsonStringEnumMemberName("active")]
    Active = 1,

    /// <summary>A known wait: a human answer, a reviewer, CI, a child workflow, the heavy-job lock. Never a stall (design spec §9.2).</summary>
    [JsonStringEnumMemberName("wait")]
    Wait = 2,

    /// <summary>The workflow is finished.</summary>
    [JsonStringEnumMemberName("terminal")]
    Terminal = 3,
}
