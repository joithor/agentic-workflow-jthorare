import { spawnSync } from "node:child_process";
import fs from "node:fs";

import { compare, type Mismatch } from "./compare.js";
import { makeSandbox, runCase, type RunOptions } from "./run.js";
import { parseSuite, type Suite } from "./schema.js";

export interface CaseResult {
  name: string;
  mismatches: Mismatch[];
}

export interface StartupResult {
  cli: string;
  samplesMs: number[];
  medianMs: number;
  budgetMs: number;
  ok: boolean;
}

export interface SuiteReport {
  suite: string;
  cases: CaseResult[];
  startup?: StartupResult;
  ok: boolean;
}

export function loadSuite(file: string): Suite {
  let raw: unknown;
  try {
    raw = JSON.parse(fs.readFileSync(file, "utf8"));
  } catch (e) {
    throw new Error(`${file}: ${(e as Error).message}`);
  }
  const r = parseSuite(raw);
  if (!r.ok) throw new Error(`${file}: ${r.error}`);
  return r.suite;
}

export function saveSuite(file: string, suite: Suite): void {
  fs.writeFileSync(file, `${JSON.stringify(suite, null, 2)}\n`);
}

export function median(xs: readonly number[]): number {
  const s = [...xs].sort((a, b) => a - b);
  const mid = Math.floor(s.length / 2);
  return s.length % 2 === 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
}

// Startup budget (spec §4c): the median wall time of N runs of one command in
// a fresh sandbox, against the suite's declared budget.
export function measureStartup(suite: Suite, opts: RunOptions, now: () => number = () => performance.now()): StartupResult | undefined {
  const b = suite.startupBudget;
  if (b === undefined) return undefined;
  const sb = makeSandbox(opts);
  try {
    const samplesMs: number[] = [];
    for (let i = 0; i < b.runs; i++) {
      const t0 = now();
      spawnSync(sb.shim(b.cli), b.args, { env: sb.env, input: "", encoding: "utf8" });
      samplesMs.push(Math.round(now() - t0));
    }
    const m = median(samplesMs);
    return { cli: b.cli, samplesMs, medianMs: m, budgetMs: b.medianMsMax, ok: m <= b.medianMsMax };
  } finally {
    fs.rmSync(sb.root, { recursive: true, force: true });
  }
}

// Record: run every case against the given (TS) targets and store what they did.
export function recordSuite(suite: Suite, opts: Omit<RunOptions, "mode">): Suite {
  const cases = suite.cases.map((c) => {
    const { observed, models } = runCase(c, suite.normalize, { ...opts, mode: "record" });
    const { misses, ...expect } = observed;
    if (misses.length > 0) throw new Error(`case ${c.name}: unrecorded model call(s) ${misses.join(", ")}; add a scripted response to its "models"`);
    return { ...c, models, expect };
  });
  return { ...suite, cases };
}

export function replaySuite(suite: Suite, opts: Omit<RunOptions, "mode">, now?: () => number): SuiteReport {
  const cases = suite.cases.map((c): CaseResult => {
    if (c.expect === undefined) return { name: c.name, mismatches: [{ field: "expect", diff: "no golden recorded; run `golden record` first" }] };
    const { observed } = runCase(c, suite.normalize, { ...opts, mode: "replay" });
    return { name: c.name, mismatches: compare(c.expect, observed) };
  });
  const startup = measureStartup(suite, { ...opts, mode: "replay" }, now);
  return { suite: suite.name, cases, startup, ok: cases.every((c) => c.mismatches.length === 0) && (startup?.ok ?? true) };
}

export function formatReport(r: SuiteReport): string {
  const lines = [`suite ${r.suite}: ${r.ok ? "PASS" : "FAIL"}`];
  for (const c of r.cases) {
    lines.push(`  ${c.mismatches.length === 0 ? "PASS" : "FAIL"} ${c.name}`);
    for (const m of c.mismatches) lines.push(`    ${m.field}:`, ...m.diff.split("\n").map((l) => `      ${l}`));
  }
  if (r.startup !== undefined) {
    const s = r.startup;
    lines.push(`  ${s.ok ? "PASS" : "FAIL"} startup ${s.cli}: median ${s.medianMs} ms (budget ${s.budgetMs} ms, samples ${s.samplesMs.join(",")})`);
  }
  return lines.join("\n");
}
