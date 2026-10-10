import crypto from "node:crypto";
import fs from "node:fs";

import type { ModelResponse } from "./schema.js";

// The fake model provider (spec §4c): a stub installed as claude, codex and
// cursor-agent at the front of PATH. It replays responses keyed by a digest of
// its argv and fails loudly on anything it hasn't seen, so a changed prompt or
// input can never silently pass. In record mode it hands out the case's
// scripted responses in order and keys each by the input it answered.
export interface ModelStore {
  mode: "record" | "replay";
  scripted: ModelResponse[];
  responses: ModelResponse[];
  misses: string[];
}

export const MISS_EXIT = 97;

export function argsDigest(bin: string, args: readonly string[]): string {
  return crypto.createHash("sha256").update(JSON.stringify([bin, ...args])).digest("hex").slice(0, 16);
}

export function readStore(file: string): ModelStore {
  return JSON.parse(fs.readFileSync(file, "utf8")) as ModelStore;
}

export function writeStore(file: string, s: ModelStore): void {
  fs.writeFileSync(file, JSON.stringify(s, null, 2));
}

export function fakeModel(bin: string, args: readonly string[], file: string): { stdout: string; stderr: string; exitCode: number } {
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
      hit = { ...s.scripted[i], argsDigest: digest };
      s.scripted.splice(i, 1);
      s.responses.push(hit);
    }
  }
  if (hit === undefined) {
    s.misses.push(`${bin} ${digest}`);
    writeStore(file, s);
    return { stdout: "", stderr: `golden-fake-model: unrecorded ${bin} input ${digest} (${s.mode} mode)`, exitCode: MISS_EXIT };
  }
  writeStore(file, s);
  return { stdout: hit.stdout, stderr: "", exitCode: hit.exitCode };
}
