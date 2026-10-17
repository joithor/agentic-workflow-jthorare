using System.Text.Json.Serialization;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>
/// What `sindri onyx up` ended with. JSON: `{ port, url, version, warnings: [string], steps: [{ step, outcome }] }` with `outcome` one of `ran`, `skipped`, `needs-human`,
/// one entry per step in the order they run (fetch, config, pull, start).
/// </summary>
/// <param name="Port">The loopback port Onyx's web server is on.</param>
/// <param name="Url">http://127.0.0.1:&lt;port&gt;.</param>
/// <param name="Version">The release the running Onyx reports.</param>
/// <param name="Warnings">Preflight checks that warned and didn't block.</param>
/// <param name="Steps">What each step did, in order.</param>
/// <param name="Key">The pointer to Sindri's key (`keychain:onyx/sindri-onyx` or the `file:` fallback; never the key), once the key step made sure of one.</param>
/// <param name="Profile">`approved` or `written-unapproved`, set by the command after the profile step (the service leaves it null).</param>
/// <param name="Doctor">The `context:onyx` doctor row the command ran after approval; null until then.</param>
/// <param name="Connectors">The address of Onyx's connectors page the command opened; null until then.</param>
public sealed record OnyxUpResponse(
    int Port,
    string Url,
    string Version,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<OnyxStepResult> Steps,
    string? Key = null,
    string? Profile = null,
    DoctorCheck? Doctor = null,
    string? Connectors = null)
{
    /// <summary>Gets the names of the steps that did work this run (a step that rewrote a file counts as ran).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Ran => [.. Steps.Where(static s => s.Outcome is OnyxStepOutcome.Ran or OnyxStepOutcome.Rewrote).Select(static s => s.Step)];

    /// <summary>Gets the names of the steps whose inputs and results were already as needed.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Skipped => Named(OnyxStepOutcome.Skipped);

    /// <summary>Gets the names of the steps that wait for the human. Reserved for P3's account setup: no step produces it yet, so it is empty today.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> NeedsHuman => Named(OnyxStepOutcome.NeedsHuman);

    private List<string> Named(OnyxStepOutcome outcome) => [.. Steps.Where(s => s.Outcome == outcome).Select(static s => s.Step)];
}
