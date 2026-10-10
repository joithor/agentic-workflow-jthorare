import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

// The call recorder for the replay week (spec §4c, B9): a wrapper a hook or a
// CLI call can be pointed at. It records inputs only (argv, an allowlisted
// slice of the environment, stdin), scrubbed by the existing scrubber CLI so
// there is one pattern source, then the wrapped command runs unchanged. If the
// scrubber can't run, nothing is recorded (fail closed for the recording,
// never for the call).
export const DEFAULT_ENV_ALLOWLIST = ["AW_SESSION_ID", "AW_PROVIDER", "AW_PROMPT_SORT_BUDGET_MS"] as const;

export type Scrub = (text: string) => string | null;

export function recordingsDir(env: NodeJS.ProcessEnv): string {
  const state = env.AW_STATE_DIR !== undefined && env.AW_STATE_DIR !== "" ? env.AW_STATE_DIR : path.join(os.homedir(), ".agentic-workflow");
  return path.join(state, "golden", "recordings");
}

// One scrubber call per record: the fields joined by a separator line no
// pattern can match across, then split back; a changed field count means a
// match crossed a separator, so the record is dropped.
const SEP = "\n\u0000golden-record-separator\u0000\n";

export function scrubFields(fields: readonly string[], scrub: Scrub): string[] | null {
  const out = scrub(fields.join(SEP));
  if (out === null) return null;
  const parts = out.split(SEP);
  return parts.length === fields.length ? parts : null;
}

// The real scrubber: `sindri scrub` reads stdin and writes the scrubbed text to stdout.
export function sindriScrub(bin: string, env: NodeJS.ProcessEnv): Scrub {
  return (text) => {
    const r = spawnSync(bin, ["scrub"], { input: text, encoding: "utf8", env, timeout: 10_000 });
    return r.error === undefined && r.status === 0 ? r.stdout : null;
  };
}

export interface CallInput {
  name: string;
  argv: string[];
  env: NodeJS.ProcessEnv;
  stdin: string;
  allowlist: readonly string[];
}

export function recordCall(input: CallInput, scrub: Scrub, now: () => Date): { recorded: boolean; file?: string; warning?: string } {
  if (!/^[a-z0-9][a-z0-9-]{0,63}$/.test(input.name)) return { recorded: false, warning: `golden record-call: invalid --name ${JSON.stringify(input.name)}` };
  const envKeys = input.allowlist.filter((k) => input.env[k] !== undefined);
  const fields = scrubFields([...input.argv, ...envKeys.map((k) => input.env[k] as string), input.stdin], scrub);
  if (fields === null) return { recorded: false, warning: "golden record-call: scrubber unavailable or unsafe split; call not recorded" };
  const argv = fields.slice(0, input.argv.length);
  const envOut = Object.fromEntries(envKeys.map((k, i) => [k, fields[input.argv.length + i]]));
  const record = { schema: "golden-call/v1", ts: now().toISOString(), name: input.name, argv, env: envOut, stdin: fields[fields.length - 1] };
  const dir = recordingsDir(input.env);
  const file = path.join(dir, `${input.name}.jsonl`);
  try {
    fs.mkdirSync(dir, { recursive: true, mode: 0o700 });
    fs.appendFileSync(file, `${JSON.stringify(record)}\n`, { mode: 0o600 });
  } catch (e) {
    return { recorded: false, warning: `golden record-call: cannot write ${file}: ${(e as Error).message}` };
  }
  return { recorded: true, file };
}
