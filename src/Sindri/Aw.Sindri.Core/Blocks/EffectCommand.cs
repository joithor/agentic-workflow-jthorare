namespace Aw.Sindri.Core.Blocks;

/// <summary>An effect's outbox command: its kind (on the allowlist) and its payload as JSON.</summary>
/// <param name="Kind">The command kind, for example 'notify.human'.</param>
/// <param name="PayloadJson">The payload.</param>
public sealed record EffectCommand(string Kind, string PayloadJson);
