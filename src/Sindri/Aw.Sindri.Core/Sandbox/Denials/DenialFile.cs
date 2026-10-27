using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>
/// The run's denial counter, 'denials.json' in the run area (sandbox spec §3.6): the true counts the API and the app read at render.
/// At most 200 distinct (kind, target) entries; the rest are counted per kind in <see cref="Overflow"/>.
/// </summary>
/// <param name="LaunchId">The launch.</param>
/// <param name="UpdatedAt">When the file was last written.</param>
/// <param name="Entries">One entry per (kind, target, rule), sorted by kind, then target, then rule.</param>
/// <param name="Overflow">The denials past the 200-target limit, one row per kind that overflowed.</param>
public sealed record DenialFile(LaunchId LaunchId, DateTimeOffset UpdatedAt, IReadOnlyList<DenialEntry> Entries, IReadOnlyList<DenialOverflow> Overflow);
