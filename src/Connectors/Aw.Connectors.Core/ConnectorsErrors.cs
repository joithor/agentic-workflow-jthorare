using Aw.Kernel;

namespace Aw.Connectors.Core;

/// <summary>The Connectors error catalogue (codes AW-CONNECTORS-NNN).</summary>
public static class ConnectorsErrors
{
    /// <summary>AW-CONNECTORS-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-CONNECTORS-001", ErrorKind.Invalid, "Connectors input failed validation");

    /// <summary>Every Connectors error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid];
}
