using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Blocks;

/// <summary>The type of a block's input or output, which the validator matches and a signal's values are parsed by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PortType>))]
public enum PortType
{
    /// <summary>Any non-empty text.</summary>
    [JsonStringEnumMemberName("text")]
    Text = 1,

    /// <summary>A decimal number.</summary>
    [JsonStringEnumMemberName("number")]
    Number = 2,

    /// <summary>'true' or 'false'.</summary>
    [JsonStringEnumMemberName("flag")]
    Flag = 3,

    /// <summary>'pass' or 'fail'.</summary>
    [JsonStringEnumMemberName("verdict")]
    Verdict = 4,

    /// <summary>A size class: XS, S, M, L or XL.</summary>
    [JsonStringEnumMemberName("size")]
    Size = 5,

    /// <summary>A commit sha: 7 to 64 lower-case hex digits.</summary>
    [JsonStringEnumMemberName("sha")]
    Sha = 6,
}
