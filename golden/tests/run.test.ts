import fs from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { contained, darwinProfile, makeSandbox, runCase, SANDBOX_EXEC, substitute, useOsSandbox } from "../src/run.js";
import { GOLDEN_DIR as golden, kase, OPTS } from "./helpers.js";

const ALL = { normalize: ["sandbox-path", "uuid", "iso-timestamp", "epoch-ms", "duration-ms"] as const };
const NONE = { normalize: [] as const };
const darwin = fs.existsSync(SANDBOX_EXEC);

describe("substitute", () => {
  it("fills {{repo}} and {{sandbox}}", () => {
    expect(substitute("{{repo}}/a {{sandbox}}/b", { repo: "/r", sandbox: "/s" })).toBe("/r/a /s/b");
  });
});

describe("contained", () => {
  it("resolves inside the root and refuses an escape", () => {
    expect(contained("/r", "a/b")).toBe("/r/a/b");
    expect(() => contained("/r", "../x")).toThrow("path escapes its root: ../x");
    expect(() => contained("/r", "/etc/passwd")).toThrow(/escapes/);
  });
});

describe("OS sandbox", () => {
  it("denies outbound network and the keychain in its profile", () => {
    const p = darwinProfile("/Users/u");
    expect(p).toContain('(deny network-outbound (remote ip "*:*"))');
    expect(p).toContain('global-name "com.apple.SecurityServer"');
    expect(p).toContain('(subpath "/Users/u/Library/Keychains")');
  });

  it("is on wherever sandbox-exec exists unless turned off", () => {
    expect(useOsSandbox({})).toBe(darwin);
    expect(useOsSandbox({ osSandbox: false })).toBe(false);
  });

  it.runIf(darwin)("blocks a target that reaches for the network or the real keychain", () => {
    expect(runCase(kase({ args: ["network"] }), NONE, { ...OPTS, mode: "record" }).observed.stdout).toBe("network blocked EPERM\n");
    expect(runCase(kase({ args: ["keychain-real"] }), NONE, { ...OPTS, mode: "record" }).observed.stdout).toMatch(/^keychain blocked \d+\n$/);
  });
});

describe("makeSandbox", () => {
  it("builds an isolated env: sandbox HOME and state, fake model CLIs and keychain stub first on PATH", () => {
    const sb = makeSandbox({ ...OPTS, mode: "replay" }, { X: "{{sandbox}}/x", HOME: "/should/lose" });
    try {
      expect(sb.env.HOME).toBe(path.join(sb.root, "home"));
      expect(sb.env.AW_STATE_DIR).toBe(path.join(sb.root, "state"));
      expect(sb.env.PATH?.split(":")[0]).toBe(path.join(sb.root, "bin/fake"));
      expect(sb.env.X).toBe(path.join(sb.root, "x"));
      expect(Object.keys(sb.env)).not.toContain("USER");
      for (const bin of ["claude", "codex", "cursor-agent", "security"]) expect(fs.existsSync(path.join(sb.root, "bin/fake", bin))).toBe(true);
      expect(fs.readFileSync(sb.shim("t"), "utf8")).toContain(`'${golden}/tests/fixtures/target.mjs'`);
      expect(() => sb.inside("../escape")).toThrow(/escapes/);
    } finally {
      fs.rmSync(sb.root, { recursive: true, force: true });
    }
  });
});

describe("runCase", () => {
  it("captures stdout, stderr, exit code, files and sqlite rows, normalized", () => {
    const c = kase({
      args: ["echo", "a", "{{sandbox}}"], stdin: "in", env: { TEST_VAR: "v" },
      capture: { files: ["state/t/out.json", "state/t/none"], sqlite: [{ db: "state/t/db.sqlite", table: "rows", orderBy: "n" }] },
    });
    const fired = new Set<string>();
    const { observed } = runCase(c, ALL, { ...OPTS, mode: "record" }, fired as Set<never>);
    expect(observed.stdout).toBe("args=a,<SANDBOX> stdin=in id=<UUID> at=<TS> home=<SANDBOX>/home\n");
    expect(observed.stderr).toBe("env=v\n");
    expect(observed.exitCode).toBe(0);
    expect(observed.files).toEqual({ "state/t/out.json": '{"elapsed_ms":<MS>,"seen":["a","<SANDBOX>"]}', "state/t/none": null });
    expect(observed.sqlite).toEqual({ "state/t/db.sqlite#rows": ['{"id":"<UUID>","n":2}'] });
    expect(observed.misses).toEqual([]);
    expect([...fired].sort()).toEqual(["duration-ms", "iso-timestamp", "sandbox-path", "uuid"]);
  });

  it("applies setup files, setup steps (cli and script) and sql (creating its dir) before the run", () => {
    const c = kase({
      args: ["echo"],
      setup: {
        files: { "state/seed.txt": { input: "seed" } },
        steps: [{ cli: "t", args: ["echo", "first"] }, { script: "tests/fixtures/target.mjs", args: ["echo", "second", "x"] }],
        sql: [{ db: "state/t/db.sqlite", statements: ["INSERT INTO rows VALUES ('fixed', 9)"] }, { db: "state/new/other.sqlite", statements: ["CREATE TABLE z (a)"] }],
      },
      capture: { files: ["state/seed.txt"], sqlite: [{ db: "state/t/db.sqlite", table: "rows", orderBy: "n" }] },
    });
    const { observed } = runCase(c, { normalize: [], inputs: { seed: { repeat: "ab", times: 2 } } }, { ...OPTS, mode: "record" });
    expect(observed.files["state/seed.txt"]).toBe("abab");
    expect(observed.sqlite["state/t/db.sqlite#rows"]?.map((r) => (JSON.parse(r) as { n: number }).n)).toEqual([0, 1, 2, 9]);
  });

  it("throws on a setup step that exits non-zero (a broken target)", () => {
    const c = kase({ setup: { steps: [{ cli: "t", args: ["echo", "fail"] }] } });
    expect(() => runCase(c, NONE, { ...OPTS, mode: "record" })).toThrow(/^setup step 1 \(t\) exited 3: env=$/);
    const s = kase({ setup: { steps: [{ script: "tests/fixtures/does-not-exist" }] } });
    expect(() => runCase(s, NONE, { ...OPTS, mode: "record" })).toThrow(/setup step 1 \(tests\/fixtures\/does-not-exist\) exited -?\d+/);
  });

  it("runs a repo script case and reports its exit code", () => {
    const { observed } = runCase(kase({ run: { script: "tests/fixtures/target.mjs" }, args: ["echo", "fail"] }), NONE, { ...OPTS, mode: "record" });
    expect(observed.exitCode).toBe(3);
  });

  it("records a missing database and a bad table as rows, not a crash", () => {
    const c = kase({ args: ["echo"], capture: { sqlite: [{ db: "state/nope.sqlite", table: "rows", orderBy: "n" }, { db: "state/t/db.sqlite", table: "missing", orderBy: "n" }] } });
    const { observed } = runCase(c, NONE, { ...OPTS, mode: "record" });
    expect(observed.sqlite["state/nope.sqlite#rows"]).toEqual(["<missing database>"]);
    expect(observed.sqlite["state/t/db.sqlite#missing"]?.[0]).toContain("<error: no such table: missing>");
  });

  it("reports a spawn failure, a timeout and death by signal as exit -1", () => {
    const run = (script: string, timeoutMs?: number) => runCase(kase({ run: { script } }), NONE, { ...OPTS, mode: "record", timeoutMs, osSandbox: false }).observed;
    const missing = run("tests/fixtures/does-not-exist");
    expect(missing.exitCode).toBe(-1);
    expect(missing.stderr).toContain("<spawn error:");
    expect(run("tests/fixtures/sleep.sh", 200).exitCode).toBe(-1);
    expect(run("tests/fixtures/selfkill.sh").exitCode).toBe(-1);
  });

  it("keeps the real keychain out of reach through the PATH stub", () => {
    const { observed } = runCase(kase({ args: ["keychain"] }), NONE, { ...OPTS, mode: "record" });
    expect(observed.stdout).toBe("security exit 44\n");
  });

  it("records scripted model responses keyed by digest, then replays them", () => {
    const c = kase({ args: ["model"], stdin: "hello", models: [{ bin: "claude", stdout: "hi\n" }] });
    const rec = runCase(c, NONE, { ...OPTS, mode: "record" });
    expect(rec.observed.stdout).toBe("hi\n");
    expect(rec.unused).toEqual([]);
    expect(rec.models[0]?.argsDigest).toMatch(/^[0-9a-f]{16}$/);
    const rep = runCase({ ...c, models: rec.models }, NONE, { ...OPTS, mode: "replay" });
    expect(rep.observed.stdout).toBe("hi\n");
    expect(rep.observed.misses).toEqual([]);
    expect(rep.observed.unusedRecorded).toEqual([]);
  });

  it("fails loudly in replay when the model input changed, and names the recorded call that went unused", () => {
    const c = kase({ args: ["model-tolerant"], stdin: "changed", models: [{ bin: "claude", argsDigest: "0000000000000000", stdout: "hi" }, { bin: "codex", stdout: "x" }] });
    const { observed } = runCase(c, NONE, { ...OPTS, mode: "replay" });
    expect(observed.stdout).toBe("model failed\n");
    expect(observed.misses).toHaveLength(1);
    expect(observed.misses[0]).toMatch(/^claude [0-9a-f]{16}$/);
    expect(observed.unusedRecorded).toEqual(["claude 0000000000000000", "codex ?"]);
  });
});
