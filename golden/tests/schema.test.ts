import { describe, expect, it } from "vitest";

import { decodeText, parseSuite } from "../src/schema.js";

const minimal = { schema: "golden-suite/v1", name: "s", normalize: ["uuid"], cases: [{ name: "c", run: { cli: "judge" } }] };

describe("parseSuite", () => {
  it("fills defaults for a minimal suite", () => {
    const r = parseSuite(minimal);
    expect(r.ok).toBe(true);
    if (!r.ok) return;
    const c = r.suite.cases[0];
    expect(c.args).toEqual([]);
    expect(c.env).toEqual({});
    expect(c.stdin).toBe("");
    expect(c.setup).toEqual({ files: {}, steps: [], sql: [] });
    expect(c.capture).toEqual({ files: [], sqlite: [] });
    expect(c.models).toEqual([]);
    expect(c.expect).toBeUndefined();
  });

  it("accepts a script case and a full expectation", () => {
    const r = parseSuite({
      ...minimal,
      startupBudget: { cli: "judge", args: ["health"], runs: 3, medianMsMax: 900 },
      cases: [{
        name: "hook", run: { script: "config/hooks/send-gate.sh" }, stdin: { base64: "aGk=" },
        models: [{ bin: "claude", argsDigest: "ab", stdout: "{}" }],
        expect: { stdout: "x", stderr: "", exitCode: 0, files: { "state/a": null }, sqlite: { decisions: ["{}"] } },
      }],
    });
    expect(r.ok).toBe(true);
    if (r.ok) expect(r.suite.cases[0].models[0].exitCode).toBe(0);
  });

  it("rejects a wrong schema version, an unknown normalizer and duplicate case names with a path", () => {
    const bad = parseSuite({ ...minimal, schema: "golden-suite/v0" });
    expect(bad.ok).toBe(false);
    if (!bad.ok) expect(bad.error).toContain("schema");
    const rule = parseSuite({ ...minimal, normalize: ["fuzzy"] });
    expect(rule.ok).toBe(false);
    if (!rule.ok) expect(rule.error).toContain("normalize.0");
    const dup = parseSuite({ ...minimal, cases: [minimal.cases[0], minimal.cases[0]] });
    expect(dup.ok).toBe(false);
    if (!dup.ok) expect(dup.error).toContain("duplicate case name: c");
  });

  it("rejects a case that names neither a cli nor a script", () => {
    const r = parseSuite({ ...minimal, cases: [{ name: "c", run: {} }] });
    expect(r.ok).toBe(false);
  });
});

describe("decodeText", () => {
  it("passes a plain string through and decodes base64", () => {
    expect(decodeText("plain")).toBe("plain");
    expect(decodeText({ base64: Buffer.from("héllo", "utf8").toString("base64") })).toBe("héllo");
  });
});
