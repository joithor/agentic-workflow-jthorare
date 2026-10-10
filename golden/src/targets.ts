import fs from "node:fs";

import { z } from "zod";

import type { Targets } from "./run.js";

// Targets map a CLI name in a suite to the command that runs it: the TS build
// today (goldens/targets.ts.json), the C# build later (goldens/targets.cs.json,
// or --target overrides). Commands may use {{repo}}.
const TargetsSchema = z.record(z.string().regex(/^[a-z][a-z0-9-]*$/), z.array(z.string().min(1)).min(1));

export function loadTargets(file: string): Targets {
  let raw: unknown;
  try {
    raw = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (e) {
    throw new Error(`${file}: ${(e as Error).message}`);
  }
  const r = TargetsSchema.safeParse(raw);
  if (!r.success) throw new Error(`${file}: ${r.error.issues.map((i) => `${i.path.join(".") || "(root)"}: ${i.message}`).join("; ")}`);
  return r.data;
}

// --target name=cmd arg arg — split on spaces, so a path with a space needs a
// targets file (--targets) instead.
export function parseTargetOverride(spec: string): [string, string[]] {
  const i = spec.indexOf("=");
  const name = i === -1 ? "" : spec.slice(0, i);
  const cmd = spec.slice(i + 1).split(" ").filter((s) => s !== "");
  if (name === "" || cmd.length === 0) throw new Error(`--target expects name=command, got ${JSON.stringify(spec)}`);
  return [name, cmd];
}
