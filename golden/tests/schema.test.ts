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
    expect(r.suite.outputs).toBe("plain");
    expect(r.suite.inputs).toEqual({});
    expect(r.suite.startupBudgets).toEqual([]);
    expect(c.models).toEqual([]);
    expect(c.expect).toBeUndefined();
  });

  it("accepts a script case and a full expectation", () => {
    const r = parseSuite({
      ...minimal,
      startupBudgets: [{ cli: "judge", args: ["health"], runs: 3, medianMsMax: 900 }],
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

describe("suite confinement", () => {
  const withCase = (c: Record<string, unknown>) => parseSuite({ ...minimal, cases: [{ name: "c", run: { cli: "judge" }, ...c }] });
  const err = (r: ReturnType<typeof parseSuite>): string => (r.ok ? "" : r.error);

  it("rejects absolute paths and '..' in every path field", () => {
    expect(err(withCase({ capture: { files: ["../../home/u/.ssh/id_ed25519"] } }))).toContain("capture.files.0: must be a relative path without '..'");
    expect(err(withCase({ capture: { files: ["/etc/passwd"] } }))).toContain("must be a relative path");
    expect(err(withCase({ setup: { files: { "../x": "y" } } }))).toContain("must be a relative path");
    expect(err(withCase({ setup: { sql: [{ db: "/tmp/x.sqlite", statements: [] }] } }))).toContain("must be a relative path");
    expect(err(withCase({ capture: { sqlite: [{ db: "a/../../b", table: "t", orderBy: "a" }] } }))).toContain("must be a relative path");
    expect(err(withCase({ run: { script: "../outside.sh" } }))).not.toBe("");
    expect(err(withCase({ setup: { steps: [{ script: "/bin/sh" }] } }))).not.toBe("");
    expect(err(withCase({ capture: { files: ["a\\b"] } }))).toContain("must be a relative path");
  });

  it("rejects env keys the sandbox owns", () => {
    for (const k of ["HOME", "PATH", "AW_STATE_DIR", "TMPDIR", "TZ", "GOLDEN_FAKE_STORE"]) {
      expect(err(withCase({ env: { [k]: "/real" } }))).toContain("is reserved by the sandbox");
    }
    expect(withCase({ env: { AW_SESSION_ID: "s" } }).ok).toBe(true);
  });

  it("rejects an unknown input reference and an input that references another", () => {
    expect(err(withCase({ stdin: { input: "nope" } }))).toBe('case c: unknown input "nope"');
    expect(err(parseSuite({ ...minimal, inputs: { a: { input: "b" } } }))).toBe("inputs.a: an input can't reference another input");
    expect(err(parseSuite({ ...minimal, inputs: { a: "x" }, cases: [{ name: "c", run: { cli: "j" }, stdin: { concat: [{ input: "a" }, "y"] }, expect: { stdout: { input: "a" }, stderr: "", exitCode: 0, files: {}, sqlite: {} } }] }))).toBe("");
  });
});

describe("decodeText", () => {
  it("decodes plain, base64, input references, repeats and concatenations", () => {
    expect(decodeText("plain")).toBe("plain");
    expect(decodeText({ base64: Buffer.from("héllo", "utf8").toString("base64") })).toBe("héllo");
    expect(decodeText({ input: "a" }, { a: { repeat: "xy", times: 3 } })).toBe("xyxyxy");
    expect(decodeText({ input: "missing" }, {})).toBe("");
    expect(decodeText({ concat: ["a", { repeat: "b", times: 2 }, { input: "c" }] }, { c: "C" })).toBe("abbC");
  });
});
