import path from "node:path";

import { describe, expect, it } from "vitest";

import { parseRecordCallArgs, parseSuiteArgs } from "../src/args.js";

describe("parseSuiteArgs", () => {
  it("collects files, repo, targets file, overrides and the sandbox switch", () => {
    expect(parseSuiteArgs(["a.json", "--repo", "/r", "--targets", "t.json", "--target", "judge=x", "b.json", "--no-os-sandbox"], "/d")).toEqual({
      files: ["a.json", "b.json"], repo: "/r", targetsFile: "t.json", overrides: ["judge=x"], osSandbox: false,
    });
  });

  it("defaults the targets file to the repo's goldens/targets.ts.json", () => {
    expect(parseSuiteArgs(["a.json"], "/d")).toEqual({ files: ["a.json"], repo: "/d", targetsFile: path.join("/d", "goldens", "targets.ts.json"), overrides: [] });
  });

  it("rejects no files, a flag without a value and an unknown flag", () => {
    expect(parseSuiteArgs([], "/d")).toEqual({ error: "no suite files given" });
    expect(parseSuiteArgs(["a.json", "--targets"], "/d")).toEqual({ error: "--targets needs a value" });
    expect(parseSuiteArgs(["a.json", "--bogus"], "/d")).toEqual({ error: "unknown flag --bogus" });
  });
});

describe("parseRecordCallArgs", () => {
  it("reads --name, extra --env vars and the command after --", () => {
    expect(parseRecordCallArgs(["--name", "send-gate", "--env", "CLAUDE_PROJECT_DIR", "--", "hook.sh", "-x"])).toEqual({
      name: "send-gate", allowlist: ["AW_SESSION_ID", "AW_PROVIDER", "AW_PROMPT_SORT_BUDGET_MS", "CLAUDE_PROJECT_DIR"], command: ["hook.sh", "-x"],
    });
  });

  it("rejects a missing command, a missing name and a stray argument", () => {
    expect(parseRecordCallArgs(["--name", "x"])).toEqual({ error: "record-call needs -- <command>" });
    expect(parseRecordCallArgs(["--name", "x", "--"])).toEqual({ error: "record-call needs -- <command>" });
    expect(parseRecordCallArgs(["--", "cmd"])).toEqual({ error: "record-call needs --name" });
    expect(parseRecordCallArgs(["--name", "x", "oops", "--", "cmd"])).toEqual({ error: "unexpected argument oops" });
    expect(parseRecordCallArgs(["--name", "--", "cmd"])).toEqual({ error: "unexpected argument --name" });
  });
});
