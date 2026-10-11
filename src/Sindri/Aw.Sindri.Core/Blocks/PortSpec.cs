using System.Globalization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Blocks;

/// <summary>One typed input or output of a block, for example 'checks.verdict' of type verdict.</summary>
/// <param name="Name">The fact's name: dotted lower-case words.</param>
/// <param name="Type">Its type.</param>
public sealed record PortSpec(string Name, PortType Type)
{
    /// <summary>Whether <paramref name="value"/> is a valid value of this port's type.</summary>
    /// <param name="value">The text a signal set.</param>
    /// <returns>True when it parses as <see cref="Type"/>.</returns>
    public bool Accepts(string value) => Type switch
    {
        PortType.Text => value.Trim().Length > 0,
        PortType.Number => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
        PortType.Flag => OneOf(value, "true", "false"),
        PortType.Verdict => OneOf(value, "pass", "fail"),
        PortType.Size => OneOf(value, "XS", "S", "M", "L", "XL"),
        PortType.Sha => HeadSha.TryFrom(value, out _),
        _ => false,
    };

    private static bool OneOf(string value, params string[] allowed) => allowed.Contains(value, StringComparer.Ordinal);

    /// <summary>The port as 'name:type', the form the docs and listings use.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => $"{Name}:{Type.ToString().ToLowerInvariant()}";
}
