import fs from "node:fs";

import { compare, type Mismatch } from "./compare.js";
import { exec, makeSandbox, runCase, useOsSandbox, type RunOptions } from "./run.js";
import { decodeText, parseSuite, type Case, type Normalizer, type StartupBudget, type Suite, type Text } from "./schema.js";

export interface CaseResult {
  name: string;
  mismatches: Mismatch[];
}

export interface StartupResult {
  cli: string;
  samplesMs: number[];
  medianMs: number;
  budgetMs: number;
  // Runs that didn't exit 0: a start that crashes or isn't found never passes.
  failedRuns: string[];
  ok: boolean;
}

export interface SuiteReport {
  suite: string;
  cases: CaseResult[];
  startups: StartupResult[];
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
  return s.length % 2 === 1 ? (s[mid] as number) : ((s[mid - 1] as number) + (s[mid] as number)) / 2;
}

// Startup budget (spec §4c): the median wall time of N runs of one command in
// a fresh sandbox, against the declared budget. Every run must exit 0.
export function measureStartup(b: StartupBudget, suite: Pick<Suite, "inputs">, opts: RunOptions, now: () => number = () => performance.now()): StartupResult {
  const sb = makeSandbox(opts);
  const xo = { timeoutMs: opts.timeoutMs ?? 30_000, osSandbox: useOsSandbox(opts) };
  try {
    const samplesMs: number[] = [];
    const failedRuns: string[] = [];
    for (let i = 0; i < b.runs; i++) {
      const t0 = now();
      const r = exec(sb.shim(b.cli), b.args, decodeText(b.stdin, suite.inputs), sb, xo);
      samplesMs.push(Math.round(now() - t0));
      if (r.exitCode !== 0) failedRuns.push(`run ${i + 1} exited ${r.exitCode}: ${r.stderr.trim().split("\n", 1).join("")}`);
    }
    const m = median(samplesMs);
    return { cli: b.cli, samplesMs, medianMs: m, budgetMs: b.medianMsMax, failedRuns, ok: failedRuns.length === 0 && m <= b.medianMsMax };
  } finally {
    fs.rmSync(sb.root, { recursive: true, force: true });
  }
}

export interface RecordDeps {
  // True when the text holds a secret shape (record stores it base64 under outputs: "auto").
  secretShaped?: (text: string) => boolean;
}

function encodeOutput(text: string, c: Case, suite: Suite, deps: RecordDeps): Text {
  if (suite.outputs === "plain") return text;
  if (typeof c.stdin === "object" && "input" in c.stdin && decodeText(c.stdin, suite.inputs) === text && text !== "") return { input: c.stdin.input };
  // Fail safe: without a scrubber, everything is treated as secret-shaped.
  return (deps.secretShaped ?? (() => true))(text) ? { base64: Buffer.from(text, "utf8").toString("base64") } : text;
}

// Record: run every case against the given (TS) targets and store what they
// did. Refuses (with every reason at once) a case with a broken setup, an
// unrecorded model call or an unused scripted response, and a declared
// normalizer that never fired.
export function recordSuite(suite: Suite, opts: Omit<RunOptions, "mode">, deps: RecordDeps = {}): Suite {
  const errors: string[] = [];
  const fired = new Set<Normalizer>();
  const cases = suite.cases.map((c) => {
    let run;
    try {
      run = runCase(c, suite, { ...opts, mode: "record" }, fired);
    } catch (e) {
      errors.push(`case ${c.name}: ${(e as Error).message}`);
      return c;
    }
    const { misses, unusedRecorded: _u, ...expect } = run.observed;
    if (misses.length > 0) errors.push(`case ${c.name}: unrecorded model call(s) ${misses.join(", ")}; add a scripted response to its "models"`);
    if (run.unused.length > 0) errors.push(`case ${c.name}: ${run.unused.length} scripted model response(s) never called (${run.unused.map((u) => u.bin).join(", ")}); remove them`);
    return { ...c, models: run.models, expect: { ...expect, stdout: encodeOutput(expect.stdout, c, suite, deps), stderr: encodeOutput(expect.stderr, c, suite, deps) } };
  });
  const idle = suite.normalize.filter((r) => !fired.has(r));
  if (idle.length > 0) errors.push(`normalize: ${idle.join(", ")} never applied to any output; remove it from the suite`);
  if (errors.length > 0) throw new Error(errors.join("\n"));
  return { ...suite, cases };
}

export function replaySuite(suite: Suite, opts: Omit<RunOptions, "mode">, now?: () => number): SuiteReport {
  const cases = suite.cases.map((c): CaseResult => {
    if (c.expect === undefined) return { name: c.name, mismatches: [{ field: "expect", diff: "no golden recorded; run `golden record` first" }] };
    try {
      const { observed } = runCase(c, suite, { ...opts, mode: "replay" });
      return { name: c.name, mismatches: compare(c.expect, observed, suite.inputs) };
    } catch (e) {
      // A broken target fails its case; it never aborts the replay.
      return { name: c.name, mismatches: [{ field: "harness", diff: (e as Error).message }] };
    }
  });
  const startups = suite.startupBudgets.map((b) => measureStartup(b, suite, { ...opts, mode: "replay" }, now));
  return { suite: suite.name, cases, startups, ok: cases.every((c) => c.mismatches.length === 0) && startups.every((s) => s.ok) };
}

export function formatStartup(s: StartupResult): string {
  const fails = s.failedRuns.map((f) => `\n    ${f}`).join("");
  return `${s.ok ? "PASS" : "FAIL"} startup ${s.cli}: median ${s.medianMs} ms (budget ${s.budgetMs} ms, samples ${s.samplesMs.join(",")})${fails}`;
}

export function formatReport(r: SuiteReport): string {
  const lines = [`suite ${r.suite}: ${r.ok ? "PASS" : "FAIL"}`];
  for (const c of r.cases) {
    lines.push(`  ${c.mismatches.length === 0 ? "PASS" : "FAIL"} ${c.name}`);
    for (const m of c.mismatches) lines.push(`    ${m.field}:`, ...m.diff.split("\n").map((l) => `      ${l}`));
  }
  for (const s of r.startups) lines.push(`  ${formatStartup(s)}`);
  return lines.join("\n");
}
