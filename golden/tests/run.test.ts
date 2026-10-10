import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

import { makeSandbox, runCase, substitute } from "../src/run.js";
import { GOLDEN_DIR as golden, kase, OPTS } from "./helpers.js";

const ALL = ["sandbox-path", "uuid", "iso-timestamp", "epoch-ms", "duration-ms"] as const;

describe("substitute", () => {
  it("fills {{repo}} and {{sandbox}}", () => {
    expect(substitute("{{repo}}/a {{sandbox}}/b", { repo: "/r", sandbox: "/s" })).toBe("/r/a /s/b");
  });
});

describe("makeSandbox", () => {
  it("builds an isolated env: sandbox HOME and state, fake model CLIs and keychain stub first on PATH", () => {
    const sb = makeSandbox({ ...OPTS, mode: "replay" }, { X: "{{sandbox}}/x" });
    try {
      expect(sb.env.HOME).toBe(path.join(sb.root, "home"));
      expect(sb.env.AW_STATE_DIR).toBe(path.join(sb.root, "state"));
      expect(sb.env.PATH.split(":")[0]).toBe(path.join(sb.root, "bin/fake"));
      expect(sb.env.X).toBe(path.join(sb.root, "x"));
      expect(Object.keys(sb.env)).not.toContain("USER");
      for (const bin of ["claude", "codex", "cursor-agent", "security"]) expect(fs.existsSync(path.join(sb.root, "bin/fake", bin))).toBe(true);
      expect(fs.readFileSync(sb.shim("t"), "utf8")).toContain(`'${golden}/tests/fixtures/target.mjs'`);
    } finally {
      fs.rmSync(sb.root, { recursive: true, force: true });
    }
  });
});

describe("runCase", () => {
  it("captures stdout, stderr, exit code, files and sqlite rows, normalized, and removes the sandbox", () => {
    const c = kase({
      args: ["echo", "a", "{{sandbox}}"], stdin: "in", env: { GOLDEN_TEST_VAR: "v" },
      capture: { files: ["state/t/out.json", "state/t/none"], sqlite: [{ db: "state/t/db.sqlite", table: "rows", orderBy: "n" }] },
    });
    const { observed } = runCase(c, ALL, { ...OPTS, mode: "record" });
    expect(observed.stdout).toBe("args=a,<SANDBOX> stdin=in id=<UUID> at=<TS> home=<SANDBOX>/home\n");
    expect(observed.stderr).toBe("env=v\n");
    expect(observed.exitCode).toBe(0);
    expect(observed.files).toEqual({ "state/t/out.json": '{"elapsed_ms":<MS>,"seen":["a","<SANDBOX>"]}', "state/t/none": null });
    expect(observed.sqlite).toEqual({ "state/t/db.sqlite#rows": ['{"id":"<UUID>","n":2}'] });
    expect(observed.misses).toEqual([]);
  });

  it("applies setup files, setup steps (cli and script) and sql before the run", () => {
    const c = kase({
      args: ["echo"],
      setup: {
        files: { "state/seed.txt": { base64: Buffer.from("seed").toString("base64") } },
        steps: [{ cli: "t", args: ["echo", "first"] }, { script: "tests/fixtures/target.mjs", args: ["echo", "second", "x"] }],
        sql: [{ db: "state/t/db.sqlite", statements: ["INSERT INTO rows VALUES ('fixed', 9)"] }],
      },
      capture: { files: ["state/seed.txt"], sqlite: [{ db: "state/t/db.sqlite", table: "rows", orderBy: "n" }] },
    });
    const { observed } = runCase(c, [], { ...OPTS, mode: "record" });
    expect(observed.files["state/seed.txt"]).toBe("seed");
    expect(observed.sqlite["state/t/db.sqlite#rows"]?.map((r) => JSON.parse(r).n)).toEqual([0, 1, 2, 9]);
  });

  it("runs a repo script case and reports its exit code", () => {
    const { observed } = runCase(kase({ run: { script: "tests/fixtures/target.mjs" }, args: ["echo", "fail"] }), [], { ...OPTS, mode: "record" });
    expect(observed.exitCode).toBe(3);
  });

  it("records a missing database and a bad table as rows, not a crash", () => {
    const c = kase({ args: ["echo"], capture: { sqlite: [{ db: "state/nope.sqlite", table: "rows", orderBy: "n" }, { db: "state/t/db.sqlite", table: "missing", orderBy: "n" }] } });
    const { observed } = runCase(c, [], { ...OPTS, mode: "record" });
    expect(observed.sqlite["state/nope.sqlite#rows"]).toEqual(["<missing database>"]);
    expect(observed.sqlite["state/t/db.sqlite#missing"]?.[0]).toContain("<error: no such table: missing>");
  });

  it("reports a spawn failure as exit -1 with the error on stderr", () => {
    const { observed } = runCase(kase({ run: { script: "tests/fixtures/does-not-exist" } }), [], { ...OPTS, mode: "record" });
    expect(observed.exitCode).toBe(-1);
    expect(observed.stderr).toContain("<spawn error:");
  });

  it("reports a timeout as exit -1", () => {
    const c = kase({ run: { script: "tests/fixtures/sleep.sh" } });
    const { observed } = runCase(c, [], { ...OPTS, mode: "record", timeoutMs: 200 });
    expect(observed.exitCode).toBe(-1);
  });

  it("reports death by signal as exit -1", () => {
    const { observed } = runCase(kase({ run: { script: "tests/fixtures/selfkill.sh" } }), [], { ...OPTS, mode: "record" });
    expect(observed.exitCode).toBe(-1);
  });

  it("keeps the real keychain out of reach", () => {
    const { observed } = runCase(kase({ args: ["keychain"] }), [], { ...OPTS, mode: "record" });
    expect(observed.stdout).toBe("security exit 44\n");
  });

  it("records scripted model responses keyed by digest, then replays them", () => {
    const c = kase({ args: ["model"], stdin: "hello", models: [{ bin: "claude", stdout: "hi\n" }] });
    const rec = runCase(c, [], { ...OPTS, mode: "record" });
    expect(rec.observed.stdout).toBe("hi\n");
    expect(rec.unused).toEqual([]);
    expect(rec.models[0]?.argsDigest).toMatch(/^[0-9a-f]{16}$/);
    const rep = runCase({ ...c, models: rec.models }, [], { ...OPTS, mode: "replay" });
    expect(rep.observed.stdout).toBe("hi\n");
    expect(rep.observed.misses).toEqual([]);
  });

  it("fails loudly in replay when the model input changed", () => {
    const c = kase({ args: ["model-tolerant"], stdin: "changed", models: [{ bin: "claude", argsDigest: "0000000000000000", stdout: "hi" }] });
    const { observed } = runCase(c, [], { ...OPTS, mode: "replay" });
    expect(observed.stdout).toBe("model failed\n");
    expect(observed.misses).toHaveLength(1);
    expect(observed.misses[0]).toMatch(/^claude [0-9a-f]{16}$/);
  });
});
