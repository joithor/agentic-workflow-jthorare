namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// The owner-voice sentence of every (code, reason[, source]) of the sandbox launch codes (sandbox spec §8): what the Check again card, <c lang="text">sindri sandbox check</c>
/// and <c lang="text">sindri doctor</c> say, so the three never differ. The table is the golden 'Golden/Sandbox/copy.json' (a test pins them together).
/// </summary>
public static class SandboxCopy
{
    private static readonly CopyRow[] _rows =
    [
        Row("SND-LAUNCH-014", "no-bwrap", "This machine has no sandbox tool, so this Step can't run by itself. Install bubblewrap (`sudo apt install bubblewrap`), then press Check again.", command: "sudo apt install bubblewrap"),
        Row("SND-LAUNCH-014", "no-user-namespace", "The system blocks the sandbox tool. Allow bubblewrap in AppArmor, then press Check again.", command: "printf 'abi <abi/4.0>,\\ninclude <tunables/global>\\nprofile bwrap /usr/bin/bwrap flags=(unconfined) {\\n  userns,\\n  include if exists <local/bwrap>\\n}\\n' | sudo tee /etc/apparmor.d/bwrap && sudo apparmor_parser -r /etc/apparmor.d/bwrap"),
        Row("SND-LAUNCH-014", "no-user-manager", "The sandbox needs your user session manager, which isn't running. Run `loginctl enable-linger $USER`, then press Check again.", command: "loginctl enable-linger $USER"),
        Row("SND-LAUNCH-014", "no-cgroup-controllers", "The system doesn't give your account memory or CPU limits. Enable the missing limit (<which>) in systemd, then press Check again."),
        Row("SND-LAUNCH-014", "no-forwarder", "The sandbox needs `socat` to reach Sindri. Install it, then press Check again.", command: "sudo apt install socat"),
        Row("SND-LAUNCH-014", "unsupported-platform", "Steps can't run by themselves on macOS yet.", state: "gray"),
        Row("SND-LAUNCH-015", "form-none", "This agent hasn't been taught how to follow a Step's rules, so it can't run by itself. Use Claude Code or Codex, or add its policy setting."),
        Row("SND-LAUNCH-015", "form-reserved", "This agent hasn't been taught how to follow a Step's rules, so it can't run by itself. Use Claude Code or Codex, or add its policy setting."),
        Row("SND-LAUNCH-015", "tool-not-expressible", "This Step allows `<tool>`, which this agent can't restrict. Change the Step's tools, or use another agent."),
        Row("SND-LAUNCH-015", "bin-denied", "The agent program lives somewhere the sandbox hides (`<path>`). Move or reinstall it somewhere under your home or `/usr`."),
        Row("SND-LAUNCH-015", "host-not-tmux", "The Step's workspace isn't set up the way the sandbox needs. This is a Sindri fault: press Copy details and report it, then Resume."),
        Row("SND-LAUNCH-015", "worktree-outside-repo", "The Step's workspace isn't set up the way the sandbox needs. This is a Sindri fault: press Copy details and report it, then Resume."),
        Row("SND-LAUNCH-015", "no-stored-plan", "This session's sandbox record is gone, so it can't be reopened safely. Start the Step again."),
        Row("SND-LAUNCH-016", "fs-deny", "The sandbox could not hide your private files (SSH keys, Sindri's data) from the Step. Open the details, fix what they name, then Check again."),
        Row("SND-LAUNCH-016", "fs-write", "The sandbox could not give the Step its workspace to write in."),
        Row("SND-LAUNCH-016", "git-ref", "The sandbox could not let the Step commit on its own branch only."),
        Row("SND-LAUNCH-016", "env", "The sandbox found a secret in the Step's environment."),
        Row("SND-LAUNCH-016", "hooks-run", "Your safety hooks did not run inside the sandbox (`<which>`)."),
        Row("SND-LAUNCH-016", "net-forward", "The sandbox could not reach Sindri from inside."),
        Row("SND-LAUNCH-016", "net-deny", "The sandbox did not stop a connection it should block."),
        Row("SND-LAUNCH-016", "login", "The sandbox could not give the Step its sign-in without exposing yours."),
        Row("SND-LAUNCH-017", "no-login", "Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again.", source: "owner-copy"),
        Row("SND-LAUNCH-017", "no-login", "Steps have their own Claude Code (or Codex) sign-in, and it's missing or has expired. Run `sindri sandbox login <target>`, then press Check again.", source: "separate-login", command: "sindri sandbox login <target>"),
        Row("SND-LAUNCH-017", "expired", "Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again.", source: "owner-copy"),
        Row("SND-LAUNCH-017", "expired", "Steps have their own Claude Code (or Codex) sign-in, and it's missing or has expired. Run `sindri sandbox login <target>`, then press Check again.", source: "separate-login", command: "sindri sandbox login <target>"),
        Row("SND-LAUNCH-017", "source-unsupported", "The sign-in method chosen for Steps (`<source>`) isn't available for `<target>`. Change `sandbox.login.source` in the profile, then press Check again."),
        Row("SND-LAUNCH-018", "no-key", "Sindri couldn't get a short-lived GitHub token for this Step: the agent GitHub key is missing. Retry the Step, or do it at your desk."),
        Row("SND-LAUNCH-018", "http-<status>", "Sindri couldn't get a short-lived GitHub token for this Step: GitHub refused the request (`<status>`). Retry the Step, or do it at your desk."),
        Row("SND-LAUNCH-018", "no-network", "Sindri couldn't get a short-lived GitHub token for this Step: there is no network to GitHub. Retry the Step, or do it at your desk."),
        Row("SND-LAUNCH-019", "memory", "This Step ran out of memory (`<limit>`) after `<time>`. Its work so far is kept on branch `<name>`. Raise `sandbox.limits.memory` in the profile (now `<limit>`; for example `<1.5 ×>`; a re-approval), then run the Step again; or do it at your desk."),
        Row("SND-LAUNCH-019", "wall", "This Step ran out of time after `<time>`. Its work so far is kept on branch `<name>`. Run it again, or do it at your desk."),
        Row("SND-LAUNCH-020", "sessions", "Waiting for a free session slot (`<n>` of `<cap>` running). This Step starts by itself.", state: "wait"),
        Row("SND-LAUNCH-021", "install-failed", "Sindri couldn't prepare this repo's packages for the Step (the install failed). Open the details, fix what they name, then run the Step again; or do it at your desk."),
        Row("SND-LAUNCH-021", "lockfile-missing", "Sindri couldn't prepare this repo's packages for the Step (a package list is missing). Open the details, fix what they name, then run the Step again; or do it at your desk."),
        Row("SND-LAUNCH-021", "timeout", "Sindri couldn't prepare this repo's packages for the Step (it took too long). Open the details, fix what they name, then run the Step again; or do it at your desk."),
    ];

    /// <summary>The sentence for a blocker, with the named values the sentence leaves open ('&lt;which&gt;', '&lt;tool&gt;', '&lt;path&gt;', '&lt;source&gt;', '&lt;target&gt;') filled where <paramref name="values"/> has them.</summary>
    /// <param name="code">A code such as 'SND-LAUNCH-014'.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="source">The credential source ('owner-copy' or 'separate-login') for -017, or null.</param>
    /// <param name="values">Named values by name without the angle brackets, or null.</param>
    /// <returns>The sentence, its copyable command (or null) and its state ('gray', 'wait' or null); null when the pair has no sentence.</returns>
    public static OwnerCopy? For(string code, string reason, string? source = null, IReadOnlyDictionary<string, string>? values = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(reason);
        var key = reason.StartsWith("http-", StringComparison.Ordinal) ? "http-<status>" : reason;
        var row = _rows.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.Ordinal) && string.Equals(r.Reason, key, StringComparison.Ordinal) && (r.Source is null || string.Equals(r.Source, source, StringComparison.Ordinal)));
        if (row is null)
        {
            return null;
        }

        var filled = values is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(values, StringComparer.Ordinal);
        if (key.Length != reason.Length)
        {
            filled["status"] = reason["http-".Length..];
        }

        return new OwnerCopy(Fill(row.Text, filled), row.Command is null ? null : Fill(row.Command, filled), row.State);
    }

    private static CopyRow Row(string code, string reason, string text, string? source = null, string? command = null, string? state = null) => new(code, reason, source, text, command, state);

    private static string Fill(string text, Dictionary<string, string> values) =>
        values.Aggregate(text, static (t, v) => t.Replace("<" + v.Key + ">", v.Value, StringComparison.Ordinal));

    private sealed record CopyRow(string Code, string Reason, string? Source, string Text, string? Command, string? State);
}

/// <summary>One owner sentence.</summary>
/// <param name="Text">The sentence, in the Step wording.</param>
/// <param name="Command">The copyable second line, or null.</param>
/// <param name="State">'gray' (not yet supported, never red), 'wait' (a waiting state, never red), or null.</param>
public sealed record OwnerCopy(string Text, string? Command, string? State);
