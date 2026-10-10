import crypto from "node:crypto";
import fs from "node:fs";

import { z } from "zod";

// The fake model provider (spec §4c): a stub installed as claude, codex and
// cursor-agent at the front of PATH. It replays responses keyed by a digest of
// its argv and fails loudly on anything it hasn't seen, so a changed prompt or
// input can never silently pass. In record mode it hands out the case's
// scripted responses in order and keys each by the input it answered. It
// notes every response it serves, so replay can also fail a recorded call the
// build never made.
const Response = z.object({ bin: z.string(), argsDigest: z.string().optional(), stdout: z.string(), exitCode: z.number().int() }).strict();
const Store = z.object({
  mode: z.enum(["record", "replay"]),
  scripted: z.array(Response),
  responses: z.array(Response),
  misses: z.array(z.string()),
  used: z.array(z.string()),
}).strict();
export type ModelStore = z.infer<typeof Store>;

export const MISS_EXIT = 97;

export function argsDigest(bin: string, args: readonly string[]): string {
  return crypto.createHash("sha256").update(JSON.stringify([bin, ...args])).digest("hex").slice(0, 16);
}

export function readStore(file: string): ModelStore {
  const r = Store.safeParse(JSON.parse(fs.readFileSync(file, "utf8")));
  if (!r.success) throw new Error(`${file}: ${r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ")}`);
  return r.data;
}

export function writeStore(file: string, s: ModelStore): void {
  fs.writeFileSync(file, JSON.stringify(s, null, 2));
}

// A model argv that names a sandbox path (codex's output-schema temp file) is
// keyed with the sandbox root and mkdtemp's random 6-char suffix replaced, so
// the key is the same in every sandbox.
const TMP_SUFFIX = /(<SANDBOX>\/tmp\/[^/\s"]*?-)[A-Za-z0-9]{6}(?=[/\s"]|$)/g;

export function sandboxFree(args: readonly string[], roots: readonly string[]): string[] {
  return args.map((a) => roots.filter((r) => r !== "").reduce((acc, r) => acc.replaceAll(r, "<SANDBOX>"), a).replace(TMP_SUFFIX, "$1XXXXXX"));
}

export function fakeModel(bin: string, rawArgs: readonly string[], file: string, roots: readonly string[] = []): { stdout: string; stderr: string; exitCode: number } {
  const args = sandboxFree(rawArgs, roots);
  let s: ModelStore;
  try {
    s = readStore(file);
  } catch (e) {
    return { stdout: "", stderr: `golden-fake-model: cannot read store ${file}: ${(e as Error).message}`, exitCode: MISS_EXIT };
  }
  const digest = argsDigest(bin, args);
  let hit = s.responses.find((r) => r.bin === bin && r.argsDigest === digest);
  if (hit === undefined && s.mode === "record") {
    const i = s.scripted.findIndex((r) => r.bin === bin);
    if (i !== -1) {
      hit = { ...(s.scripted[i] as ModelStore["scripted"][number]), argsDigest: digest };
      s.scripted.splice(i, 1);
      s.responses.push(hit);
    }
  }
  if (hit === undefined) {
    s.misses.push(`${bin} ${digest}`);
    writeStore(file, s);
    return { stdout: "", stderr: `golden-fake-model: unrecorded ${bin} input ${digest} (${s.mode} mode)`, exitCode: MISS_EXIT };
  }
  s.used.push(`${bin} ${digest}`);
  writeStore(file, s);
  return { stdout: hit.stdout, stderr: "", exitCode: hit.exitCode };
}
