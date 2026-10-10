import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { formatReport, loadSuite, measureStartup, median, recordSuite, replaySuite, saveSuite } from "../src/suite.js";
import { parseSuite, type Suite } from "../src/schema.js";
import { kase, OPTS } from "./helpers.js";

const suiteOf = (cases: ReturnType<typeof kase>[], extra: Record<string, unknown> = {}): Suite => {
  const r = parseSuite({ schema: "golden-suite/v1", name: "s", normalize: ["sandbox-path", "uuid", "iso-timestamp"], cases, ...extra });
  if (!r.ok) throw new Error(r.error);
  return r.suite;
};
const tmp = (): string => fs.mkdtempSync(path.join(os.tmpdir(), "golden-suite-"));
const budget = (o: Record<string, unknown> = {}) => ({ cli: "t", args: ["echo"], stdin: "", runs: 1, medianMsMax: 60_000, ...o });

describe("loadSuite / saveSuite", () => {
  it("round-trips a suite through a file", () => {
    const file = path.join(tmp(), "s.json");
    const s = suiteOf([kase({ args: ["echo"] })]);
    saveSuite(file, s);
    expect(fs.readFileSync(file, "utf8").endsWith("}\n")).toBe(true);
    expect(loadSuite(file)).toEqual(s);
  });

  it("names the file on unreadable JSON and on a schema error", () => {
    const dir = tmp();
    fs.writeFileSync(path.join(dir, "bad.json"), "{");
    expect(() => loadSuite(path.join(dir, "bad.json"))).toThrow(/bad\.json: /);
    fs.writeFileSync(path.join(dir, "v0.json"), JSON.stringify({ schema: "golden-suite/v0" }));
    expect(() => loadSuite(path.join(dir, "v0.json"))).toThrow(/v0\.json: schema/);
  });
});

describe("median", () => {
  it("handles odd and even sample counts", () => {
    expect(median([5, 1, 3])).toBe(3);
    expect(median([4, 1, 3, 2])).toBe(2.5);
  });
});

describe("measureStartup", () => {
  it("compares the median to the budget", () => {
    let t = 0;
    const r = measureStartup(budget({ runs: 3, medianMsMax: 40 }), { inputs: {} }, { ...OPTS, mode: "replay" }, () => (t += 50));
    expect(r).toEqual({ cli: "t", samplesMs: [50, 50, 50], medianMs: 50, budgetMs: 40, failedRuns: [], ok: false });
  });

  it("never passes a start that fails, however fast", () => {
    const r = measureStartup(budget({ args: ["echo", "fail"], runs: 2 }), { inputs: {} }, { ...OPTS, mode: "replay" });
    expect(r.ok).toBe(false);
    expect(r.failedRuns).toEqual(["run 1 exited 3: env=", "run 2 exited 3: env="]);
    const missing = measureStartup(budget({ cli: "nope" }), { inputs: {} }, { ...OPTS, mode: "replay", osSandbox: false });
    expect(missing.ok).toBe(false);
    expect(missing.failedRuns[0]).toMatch(/^run 1 exited -1: /);
  });

  it("measures with the real clock by default", () => {
    const r = measureStartup(budget(), { inputs: {} }, { ...OPTS, mode: "replay" });
    expect(r.ok).toBe(true);
    expect(r.samplesMs[0]).toBeGreaterThan(0);
  });
});

describe("recordSuite / replaySuite", () => {
  it("records expectations that the same build then replays clean, budgets included", () => {
    const recorded = recordSuite(suiteOf([kase({ args: ["echo", "x"], stdin: "s" })], { startupBudgets: [budget()] }), OPTS);
    expect(recorded.cases[0]?.expect?.stdout).toBe("args=x stdin=s id=<UUID> at=<TS> home=<SANDBOX>/home\n");
    const r = replaySuite(recorded, OPTS, () => 0);
    expect(r.ok).toBe(true);
    expect(formatReport(r)).toBe("suite s: PASS\n  PASS c\n  PASS startup t: median 0 ms (budget 60000 ms, samples 0)");
  });

  it("refuses a declared normalizer that never fires", () => {
    expect(() => recordSuite(suiteOf([kase({ args: ["keychain"] })], { normalize: ["uuid", "epoch-ms"] }), OPTS)).toThrow("normalize: uuid, epoch-ms never applied to any output; remove it from the suite");
  });

  it("stores outputs by reference, base64 or plain under outputs: auto", () => {
    const s = suiteOf(
      [kase({ name: "same", run: { script: "tests/fixtures/cat.sh" }, stdin: { input: "a" } }), kase({ name: "secret", args: ["keychain"] })].map((c, i) => ({ ...c, name: ["same", "secret"][i] as string })),
      { normalize: [], outputs: "auto", inputs: { a: "hello" } },
    );
    const rec = recordSuite(s, OPTS, { secretShaped: (t) => t.includes("44") });
    expect(rec.cases[0]?.expect?.stdout).toEqual({ input: "a" });
    expect(rec.cases[0]?.expect?.stderr).toBe("");
    expect(rec.cases[1]?.expect?.stdout).toEqual({ base64: Buffer.from("security exit 44\n").toString("base64") });
    expect(replaySuite(rec, OPTS).ok).toBe(true);
    // Without a scrubber, auto fails safe to base64.
    expect(recordSuite(s, OPTS).cases[1]?.expect?.stderr).toEqual({ base64: "" });
  });

  it("refuses to record a case with an unrecorded model call, an unused scripted response or a broken setup", () => {
    const bad = suiteOf([
      { ...kase({ args: ["model-tolerant"] }), name: "a" },
      { ...kase({ args: ["echo"], models: [{ bin: "codex", stdout: "" }] }), name: "b" },
      { ...kase({ setup: { steps: [{ cli: "t", args: ["echo", "fail"] }] } }), name: "c" },
    ], { normalize: [] });
    let message = "";
    try {
      recordSuite(bad, OPTS);
    } catch (e) {
      message = (e as Error).message;
    }
    expect(message).toMatch(/^case a: unrecorded model call\(s\) claude [0-9a-f]{16}; add a scripted response/);
    expect(message).toContain("case b: 1 scripted model response(s) never called (codex); remove them");
    expect(message).toContain("case c: setup step 1 (t) exited 3");
  });

  it("fails a case with no golden, a changed output or a broken target, and an over-budget startup — and keeps going", () => {
    const recorded = recordSuite(suiteOf([kase({ args: ["echo", "x"] })]), OPTS);
    const first = recorded.cases[0] as Suite["cases"][number];
    const changed = {
      ...recorded,
      startupBudgets: [budget({ medianMsMax: 1 })],
      cases: [{ ...first, args: ["echo", "y"] }, { ...kase({ args: ["echo"] }), name: "new" }, { ...first, name: "broken", setup: { ...first.setup, steps: [{ cli: "t", args: ["echo", "fail"], stdin: "" }] } }, { ...first, name: "after" }],
    };
    let t = 0;
    const r = replaySuite(changed, OPTS, () => (t += 10));
    expect(r.ok).toBe(false);
    const text = formatReport(r);
    expect(text).toContain("suite s: FAIL\n  FAIL c\n    stdout:\n      line 1:\n        - args=x");
    expect(text).toContain("        + args=y");
    expect(text).toContain("  FAIL new\n    expect:\n      no golden recorded; run `golden record` first");
    expect(text).toContain("  FAIL broken\n    harness:\n      setup step 1 (t) exited 3: env=");
    expect(text).toContain("  PASS after");
    expect(text).toContain("  FAIL startup t: median 10 ms (budget 1 ms, samples 10)");
  });
});
