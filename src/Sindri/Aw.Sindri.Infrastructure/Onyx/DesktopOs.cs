namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The desktop families that have a command to open an address.</summary>
internal enum DesktopOs
{
    /// <summary>No known command.</summary>
    Unsupported,

    /// <summary>macOS: `open`.</summary>
    MacOS,

    /// <summary>Linux: `xdg-open`.</summary>
    Linux,
}
