namespace Aw.Sindri.Core.Onyx;

/// <summary>Opens a page in the person's browser. The caller prints the address either way, so a missing browser costs a copy and paste.</summary>
public interface IBrowser
{
    /// <summary>Asks the desktop to open an http address (macOS `open`, Linux `xdg-open`).</summary>
    /// <param name="url">The address; only http and https are opened.</param>
    /// <returns>True when the tool ran and exited 0; false when there is no tool, it failed or it timed out.</returns>
    public bool Open(Uri url);
}
