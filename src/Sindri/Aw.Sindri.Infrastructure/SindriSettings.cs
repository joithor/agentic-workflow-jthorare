namespace Aw.Sindri.Infrastructure;

/// <summary>Sindri's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="Home">Sindri's state directory (0700): the ledger, approvals, the endpoint record and logs.</param>
/// <param name="ProfilePath">The live profile file.</param>
/// <param name="LinearKey">The Linear API key for the context engine's Linear source (LINEAR_API_KEY), or null.</param>
/// <param name="UserHome">The user's home directory, for '~/' in profile paths.</param>
internal sealed record SindriSettings(string Home, string ProfilePath, string? LinearKey = null, string UserHome = "")
{
    /// <summary>The ledger database.</summary>
    public string LedgerPath => Path.Combine(Home, "ledger.db");

    /// <summary>Approved profile snapshots, one directory per hash.</summary>
    public string ApprovalsDirectory => Path.Combine(Home, "approved");

    /// <summary>The control plane's endpoint record.</summary>
    public string EndpointPath => Path.Combine(Home, "control.json");

    // The generated ToString would print the Linear key; this one says only whether it is set.
    public override string ToString() =>
        $"SindriSettings {{ Home = {Home}, ProfilePath = {ProfilePath}, LinearKey = {(LinearKey is null ? "unset" : "set")}, UserHome = {UserHome} }}";
}
