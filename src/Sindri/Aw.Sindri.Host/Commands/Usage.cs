namespace Aw.Sindri.Host.Commands;

/// <summary>The usage text.</summary>
internal static class Usage
{
    internal static readonly string Text = string.Join('\n',
        "usage: sindri [--headless]           start the control plane if it isn't running, then print its summary",
        "       sindri serve                  run the control plane in the foreground (what 'sindri' starts; launchd target)",
        "       sindri status [--json]        the running control plane's status (exit 1 when it isn't running)",
        "       sindri stop                   stop it and release the host lease",
        "       sindri profile validate [--json]",
        "       sindri profile approve [<hash>]   without a hash: show the hash and what changed; with it: approve (needs a terminal)",
        "       sindri doctor [--json]        lease, ledger, profile and api checks; runs without the control plane",
        "       sindri workflow <verb> …      the workflow engine (sindri workflow help for the verbs)",
        "exit codes: 0 ok, 1 attention needed, 2 error");

    internal static readonly string Workflow = string.Join('\n',
        "usage: sindri workflow list [--all] [--json]",
        "       sindri workflow show <id> [--json]",
        "       sindri workflow start <recipe> <item> [--attempt N] [--base SHA] [--json]",
        "       sindri workflow signal <id> <on> [--set name=value]… [--finding F]… [--empty-diff] [--sha SHA]",
        "                              [--expect-version N] [--tokens N] [--turns N] [--json]",
        "       sindri workflow evidence <id> <kind> <sha> <digest> [--json]",
        "       sindri workflow cancel <id> [--reason TEXT] [--json]",
        "       sindri workflow park <id> <rounds|budget|no-progress|timeout|needs-answer|spec-conflict> [--detail TEXT] [--json]",
        "       sindri workflow unpark <id> [--tokens N] [--turns N] [--json]",
        "       sindri workflow takeover <id> | handback <id> [--json]",
        "       sindri workflow recipes [--json] | blocks [--json]",
        "       sindri workflow validate <file.yaml> [--json]   validate a recipe file (runs without the control plane)",
        "exit codes: 0 ok, 1 attention needed (parked, escalated, not running), 2 error");
}
