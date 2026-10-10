import fs from "node:fs";

import type { Targets } from "./run.js";

// Targets map a CLI name in a suite to the command that runs it: the TS build
// today (goldens/targets.ts.json), the C# build later (a second file, or
// --target overrides). Commands may use {{repo}}.
export function loadTargets(file: string): Targets {
  const raw = JSON.parse(fs.readFileSync(file, "utf8")) as unknown;
  if (raw === null || typeof raw !== "object" || Array.isArray(raw)) throw new Error(`${file}: expected an object of name -> [command, ...args]`);
  const out: Targets = {};
  for (const [name, cmd] of Object.entries(raw)) {
    if (!Array.isArray(cmd) || cmd.length === 0 || !cmd.every((c) => typeof c === "string")) throw new Error(`${file}: target ${name} must be a non-empty string array`);
    out[name] = cmd;
  }
  return out;
}

// --target name=cmd arg arg (split on spaces; use a targets file for anything fancier).
export function parseTargetOverride(spec: string): [string, string[]] {
  const i = spec.indexOf("=");
  const name = i === -1 ? "" : spec.slice(0, i);
  const cmd = spec.slice(i + 1).split(" ").filter((s) => s !== "");
  if (name === "" || cmd.length === 0) throw new Error(`--target expects name=command, got ${JSON.stringify(spec)}`);
  return [name, cmd];
}
