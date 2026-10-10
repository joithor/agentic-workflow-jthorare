import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { loadTargets, parseTargetOverride } from "../src/targets.js";

function file(content: string): string {
  const f = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "golden-t-")), "targets.json");
  fs.writeFileSync(f, content);
  return f;
}

describe("loadTargets", () => {
  it("reads name -> command arrays", () => {
    expect(loadTargets(file('{"judge":["node","{{repo}}/judge/dist/cli.js"]}'))).toEqual({ judge: ["node", "{{repo}}/judge/dist/cli.js"] });
  });

  it("rejects a non-object and a bad command", () => {
    expect(() => loadTargets(file("[]"))).toThrow(/expected an object/);
    expect(() => loadTargets(file("null"))).toThrow(/expected an object/);
    expect(() => loadTargets(file('{"j":[]}'))).toThrow(/target j must be a non-empty string array/);
    expect(() => loadTargets(file('{"j":"node"}'))).toThrow(/target j/);
    expect(() => loadTargets(file('{"j":[1]}'))).toThrow(/target j/);
  });
});

describe("parseTargetOverride", () => {
  it("splits name=command args", () => {
    expect(parseTargetOverride("judge=./judge-cs  --fast")).toEqual(["judge", ["./judge-cs", "--fast"]]);
  });

  it("rejects a spec without a name or a command", () => {
    expect(() => parseTargetOverride("judge")).toThrow(/--target expects name=command/);
    expect(() => parseTargetOverride("=x")).toThrow(/--target/);
    expect(() => parseTargetOverride("j=")).toThrow(/--target/);
  });
});
