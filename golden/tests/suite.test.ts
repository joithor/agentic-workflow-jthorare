import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { formatReport, loadSuite, measureStartup, median, recordSuite, replaySuite, saveSuite } from "../src/suite.js";
import type { Suite } from "../src/schema.js";
import { kase, OPTS } from "./helpers.js";

const suiteOf = (cases: ReturnType<typeof kase>[], extra: Partial<Suite> = {}): Suite => ({ schema: "golden-suite/v1", name: "s", normalize: ["sandbox-path", "uuid", "iso-timestamp"], cases, ...extra });
const tmp = (): string => fs.mkdtempSync(path.join(os.tmpdir(), "golden-suite-"));

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
  it("is undefined without a budget and compares the median to the budget", () => {
    expect(measureStartup(suiteOf([]), { ...OPTS, mode: "replay" })).toBeUndefined();
    let t = 0;
    const clock = (): number => (t += 50);
    const r = measureStartup(suiteOf([], { startupBudget: { cli: "t", args: ["echo"], runs: 3, medianMsMax: 40 } }), { ...OPTS, mode: "replay" }, clock);
    expect(r).toEqual({ cli: "t", samplesMs: [50, 50, 50], medianMs: 50, budgetMs: 40, ok: false });
  });
});

describe("recordSuite / replaySuite", () => {
  it("records expectations that the same build then replays clean", () => {
    const recorded = recordSuite(suiteOf([kase({ args: ["echo", "x"], stdin: "s" })]), OPTS);
    expect(recorded.cases[0]?.expect?.stdout).toBe("args=x stdin=s id=<UUID> at=<TS> home=<SANDBOX>/home\n");
    const r = replaySuite(recorded, OPTS);
    expect(r.ok).toBe(true);
    expect(formatReport(r)).toBe("suite s: PASS\n  PASS c");
  });

  it("stores outputs base64 when the suite asks for it", () => {
    const recorded = recordSuite(suiteOf([kase({ args: ["echo"] })], { encodeOutputs: true }), OPTS);
    expect(recorded.cases[0]?.expect?.stdout).toHaveProperty("base64");
    expect(replaySuite(recorded, OPTS).ok).toBe(true);
  });

  it("refuses to record a case with an unrecorded model call or an unused scripted response", () => {
    expect(() => recordSuite(suiteOf([kase({ args: ["model-tolerant"] })]), OPTS)).toThrow(/case c: unrecorded model call\(s\) claude [0-9a-f]{16}/);
    expect(() => recordSuite(suiteOf([kase({ args: ["echo"], models: [{ bin: "codex", stdout: "" }] })]), OPTS)).toThrow(/1 scripted model response\(s\) never called \(codex\)/);
  });

  it("fails a case with no golden, a changed output, and an over-budget startup, and formats each", () => {
    const recorded = recordSuite(suiteOf([kase({ args: ["echo", "x"] })]), OPTS);
    const changed = { ...recorded, cases: [{ ...recorded.cases[0]!, args: ["echo", "y"] }, { ...kase({ args: ["echo"] }), name: "new" }] };
    let t = 0;
    const r = replaySuite({ ...changed, startupBudget: { cli: "t", args: ["echo"], runs: 1, medianMsMax: 1 } }, OPTS, () => (t += 10));
    expect(r.ok).toBe(false);
    const text = formatReport(r);
    expect(text).toContain("suite s: FAIL\n  FAIL c\n    stdout:\n      line 1:\n        - args=x");
    expect(text).toContain("        + args=y");
    expect(text).toContain("  FAIL new\n    expect:\n      no golden recorded; run `golden record` first");
    expect(text).toContain("  FAIL startup t: median 10 ms (budget 1 ms, samples 10)");
  });

  it("measures startup with the real clock by default", () => {
    const r = measureStartup(suiteOf([], { startupBudget: { cli: "t", args: ["echo"], runs: 1, medianMsMax: 60_000 } }), { ...OPTS, mode: "replay" });
    expect(r?.ok).toBe(true);
    expect(r?.samplesMs[0]).toBeGreaterThan(0);
  });

  it("passes the startup budget when the median is within it", () => {
    let t = 0;
    const r = replaySuite(suiteOf([], { startupBudget: { cli: "t", args: [], runs: 1, medianMsMax: 100 } }), OPTS, () => (t += 10));
    expect(r.ok).toBe(true);
    expect(formatReport(r)).toContain("PASS startup t: median 10 ms");
  });
});
