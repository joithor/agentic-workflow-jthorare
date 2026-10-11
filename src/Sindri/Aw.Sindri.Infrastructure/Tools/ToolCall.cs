namespace Aw.Sindri.Infrastructure.Tools;

/// <summary>One tool invocation.</summary>
/// <param name="Name">The tool's name, for messages ('judge', 'ui-evidence').</param>
/// <param name="Binary">The binary to run.</param>
/// <param name="Arguments">Its arguments (no shell).</param>
/// <param name="Stdin">What to write to its stdin, or null for nothing.</param>
/// <param name="Timeout">How long it may run before it is stopped.</param>
/// <param name="Graceful">
/// Stop it with SIGTERM and a grace first (ui-evidence, which releases its machine locks on SIGTERM); otherwise kill its
/// whole tree at once (judge, which handles no signal and would leave its agent-CLI child running after a SIGTERM exit).
/// </param>
internal sealed record ToolCall(string Name, string Binary, IReadOnlyList<string> Arguments, byte[]? Stdin, TimeSpan Timeout, bool Graceful = false);
