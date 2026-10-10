import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { GOLDEN_DIR } from "./helpers.js";

// End to end through the built CLI (dist/cli.js, built by global-setup).
const CLI = path.join(GOLDEN_DIR, "dist", "cli.js");
const golden = (args: string[], o: { input?: string; env?: NodeJS.ProcessEnv } = {}) =>
  spawnSync(process.execPath, [CLI, ...args], { encoding: "utf8", input: o.input ?? "", env: o.env ?? process.env });

function workspace(): { dir: string; suite: string; targets: string } {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), "golden-cli-"));
  const targets = path.join(dir, "targets.json");
  fs.writeFileSync(targets, JSON.stringify({ t: ["node", "{{repo}}/tests/fixtures/target.mjs"] }));
  const suite = path.join(dir, "s.json");
  fs.writeFileSync(suite, JSON.stringify({ schema: "golden-suite/v1", name: "e2e", normalize: ["sandbox-path", "uuid", "iso-timestamp"], startupBudgets: [{ cli: "t", args: ["echo"], runs: 1, medianMsMax: 60000 }], cases: [{ name: "echo", run: { cli: "t" }, args: ["echo", "x"] }] }));
  return { dir, suite, targets };
}

describe("golden CLI", () => {
  it("records, replays clean, and fails with a precise diff and exit 1 against a changed target", () => {
    const w = workspace();
    const common = ["--repo", GOLDEN_DIR, "--targets", w.targets];
    const rec = golden(["record", w.suite, ...common]);
    expect(rec.status).toBe(0);
    expect(rec.stdout).toMatch(/^recorded 1 case\(s\) -> .*s\.json\n  PASS startup t: median \d+ ms \(budget 60000 ms, samples \d+\)\n$/);
    expect(rec.stderr).toMatch(/^golden: isolation = (sandbox-exec \(no network, no keychain\) \+ PATH stubs|PATH stubs only)\n$/);
    const ok = golden(["replay", w.suite, ...common]);
    expect(ok.status).toBe(0);
    expect(ok.stdout).toContain("suite e2e: PASS");
    const mutant = path.join(w.dir, "mutant.sh");
    fs.writeFileSync(mutant, `#!/bin/sh\nnode ${GOLDEN_DIR}/tests/fixtures/target.mjs "$@" | sed 's/args=x/args=X/'\n`, { mode: 0o755 });
    const bad = golden(["replay", w.suite, ...common, "--target", `t=${mutant}`]);
    expect(bad.status).toBe(1);
    expect(bad.stdout).toContain("  FAIL echo\n    stdout:\n      line 1:\n        - args=x");
    expect(bad.stdout).toContain("        + args=X");
  });

  it("exits 2 on usage errors, an invalid suite or a bad target", () => {
    const w = workspace();
    expect(golden([]).status).toBe(2);
    expect(golden(["replay"]).status).toBe(2);
    expect(golden(["replay", w.suite, "--bogus"]).stderr).toContain("golden: unknown flag --bogus");
    expect(golden(["record-call", "--", "true"]).status).toBe(2);
    expect(golden(["redaction-diff"]).status).toBe(2);
    fs.writeFileSync(w.suite, "{}");
    const r = golden(["replay", w.suite, "--targets", w.targets]);
    expect(r.status).toBe(2);
    expect(r.stderr).toContain("schema");
    expect(golden(["replay", w.suite, "--targets", w.targets, "--target", "nope"]).status).toBe(2);
  });

  it("exits 1 and names the case when recording hits an unrecorded model call", () => {
    const w = workspace();
    fs.writeFileSync(w.suite, JSON.stringify({ schema: "golden-suite/v1", name: "m", normalize: [], cases: [{ name: "needs-model", run: { cli: "t" }, args: ["model-tolerant"] }] }));
    const r = golden(["record", w.suite, "--repo", GOLDEN_DIR, "--targets", w.targets, "--no-os-sandbox"]);
    expect(r.status).toBe(1);
    expect(r.stderr).toContain("golden: isolation = PATH stubs only");
    expect(r.stderr).toContain("case needs-model: unrecorded model call(s) claude");
  });

  it("record-call records scrubbed inputs only and passes stdin, stdout and the exit code through", () => {
    const state = fs.mkdtempSync(path.join(os.tmpdir(), "golden-rc-"));
    const scrubber = path.join(state, "fake-sindri");
    fs.writeFileSync(scrubber, '#!/bin/sh\nsed "s/SECRET[0-9]*/[REDACTED:x]/g"\n', { mode: 0o755 });
    const env = { ...process.env, AW_STATE_DIR: state, GOLDEN_SCRUBBER: scrubber, AW_SESSION_ID: "s1" };
    const r = golden(["record-call", "--name", "probe", "--", "sh", "-c", "cat; echo out; exit 4", "SECRET1"], { input: "in SECRET2\n", env });
    expect(r.status).toBe(4);
    expect(r.stdout).toBe("in SECRET2\nout\n");
    const line = fs.readFileSync(path.join(state, "golden", "recordings", "probe.jsonl"), "utf8");
    expect(line).not.toContain("SECRET");
    expect(JSON.parse(line)).toMatchObject({ name: "probe", argv: ["sh", "-c", "cat; echo out; exit 4", "[REDACTED:x]"], env: { AW_SESSION_ID: "s1" }, stdin: "in [REDACTED:x]\n" });
    expect(golden(["record-call", "--name", "x"]).status).toBe(2);
  });

  it("redaction-diff prints the differential table", () => {
    const w = workspace();
    const exp = (s: string) => ({ stdout: s, stderr: "", exitCode: 0, files: {}, sqlite: {} });
    fs.writeFileSync(w.suite, JSON.stringify({ schema: "golden-suite/v1", name: "r", normalize: [], cases: [
      { name: "judge/a", run: { cli: "t" }, expect: exp("[REDACTED]") }, { name: "sindri/a", run: { cli: "t" }, expect: exp("[REDACTED:k]") },
    ] }));
    const r = golden(["redaction-diff", w.suite]);
    expect(r.status).toBe(0);
    expect(r.stdout).toContain("| a | 1 | 1 | yes |");
    expect(golden(["redaction-diff", path.join(w.dir, "missing.json")]).status).toBe(2);
  });
});
