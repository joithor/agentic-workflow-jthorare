import path from "node:path";

import { DEFAULT_ENV_ALLOWLIST } from "./recorder.js";

export const USAGE = `usage:
  golden record <suite.json>... [--repo DIR] [--targets FILE] [--target name=cmd] [--no-os-sandbox]
  golden replay <suite.json>... [--repo DIR] [--targets FILE] [--target name=cmd] [--no-os-sandbox]
  golden record-call --name NAME [--env VAR]... -- <command> [args...]
  golden redaction-diff <redaction.suite.json>`;

export interface SuiteArgs {
  files: string[];
  repo: string;
  targetsFile: string;
  overrides: string[];
  osSandbox?: boolean;
}

export function parseSuiteArgs(argv: readonly string[], defaultRepo: string): SuiteArgs | { error: string } {
  const out: SuiteArgs = { files: [], repo: defaultRepo, targetsFile: "", overrides: [] };
  let targetsFile: string | undefined;
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i] as string;
    const value = (): string | undefined => argv[++i];
    if (a === "--repo" || a === "--targets" || a === "--target") {
      const v = value();
      if (v === undefined) return { error: `${a} needs a value` };
      if (a === "--repo") out.repo = path.resolve(v);
      else if (a === "--targets") targetsFile = v;
      else out.overrides.push(v);
    } else if (a === "--no-os-sandbox") out.osSandbox = false;
    else if (a.startsWith("--")) return { error: `unknown flag ${a}` };
    else out.files.push(a);
  }
  if (out.files.length === 0) return { error: "no suite files given" };
  out.targetsFile = targetsFile ?? path.join(out.repo, "goldens", "targets.ts.json");
  return out;
}

export interface RecordCallArgs {
  name: string;
  allowlist: string[];
  command: string[];
}

export function parseRecordCallArgs(argv: readonly string[]): RecordCallArgs | { error: string } {
  const dash = argv.indexOf("--");
  if (dash === -1 || dash === argv.length - 1) return { error: "record-call needs -- <command>" };
  const own = argv.slice(0, dash);
  let name: string | undefined;
  const extra: string[] = [];
  for (let i = 0; i < own.length; i++) {
    const a = own[i];
    const v = own[i + 1];
    if ((a === "--name" || a === "--env") && v !== undefined) {
      if (a === "--name") name = v;
      else extra.push(v);
      i++;
    } else return { error: `unexpected argument ${String(a)}` };
  }
  if (name === undefined) return { error: "record-call needs --name" };
  return { name, allowlist: [...DEFAULT_ENV_ALLOWLIST, ...extra], command: argv.slice(dash + 1) };
}
