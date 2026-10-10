import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { argsDigest, fakeModel, readStore, writeStore } from "../src/fake-model.js";

function store(content: Parameters<typeof writeStore>[1]): string {
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "golden-fm-")), "models.json");
  writeStore(file, content);
  return file;
}

describe("argsDigest", () => {
  it("is stable for the same bin and args and changes with either", () => {
    expect(argsDigest("claude", ["-p", "x"])).toBe(argsDigest("claude", ["-p", "x"]));
    expect(argsDigest("claude", ["-p", "x"])).not.toBe(argsDigest("codex", ["-p", "x"]));
    expect(argsDigest("claude", ["-p", "x"])).toMatch(/^[0-9a-f]{16}$/);
  });
});

describe("fakeModel", () => {
  it("replays a recorded response for a known input", () => {
    const file = store({ mode: "replay", scripted: [], responses: [{ bin: "claude", argsDigest: argsDigest("claude", ["a"]), stdout: '{"ok":1}', exitCode: 0 }], misses: [], used: [] });
    expect(fakeModel("claude", ["a"], file)).toEqual({ stdout: '{"ok":1}', stderr: "", exitCode: 0 });
    expect(readStore(file).misses).toEqual([]);
    expect(readStore(file).used).toEqual([`claude ${argsDigest("claude", ["a"])}`]);
  });

  it("fails loudly on an unrecorded input in replay mode and records the miss", () => {
    const file = store({ mode: "replay", scripted: [], responses: [], misses: [], used: [] });
    const r = fakeModel("claude", ["b"], file);
    expect(r.exitCode).toBe(97);
    expect(r.stderr).toContain(`golden-fake-model: unrecorded claude input ${argsDigest("claude", ["b"])}`);
    expect(readStore(file).misses).toEqual([`claude ${argsDigest("claude", ["b"])}`]);
  });

  it("in record mode consumes the next scripted response for that bin and keys it by digest", () => {
    const file = store({ mode: "record", scripted: [{ bin: "codex", stdout: "c", exitCode: 0 }, { bin: "claude", stdout: "x", exitCode: 3 }], responses: [], misses: [], used: [] });
    expect(fakeModel("claude", ["q"], file)).toEqual({ stdout: "x", stderr: "", exitCode: 3 });
    const s = readStore(file);
    expect(s.scripted).toEqual([{ bin: "codex", stdout: "c", exitCode: 0 }]);
    expect(s.responses).toEqual([{ bin: "claude", argsDigest: argsDigest("claude", ["q"]), stdout: "x", exitCode: 3 }]);
  });

  it("in record mode reuses an already-keyed response for a repeated input", () => {
    const d = argsDigest("claude", ["q"]);
    const file = store({ mode: "record", scripted: [], responses: [{ bin: "claude", argsDigest: d, stdout: "x", exitCode: 0 }], misses: [], used: [] });
    expect(fakeModel("claude", ["q"], file).stdout).toBe("x");
  });

  it("in record mode with nothing scripted for that bin records a miss", () => {
    const file = store({ mode: "record", scripted: [], responses: [], misses: [], used: [] });
    expect(fakeModel("claude", ["q"], file).exitCode).toBe(97);
    expect(readStore(file).misses).toHaveLength(1);
  });

  it("keys an argv that names a sandbox path the same in every sandbox", () => {
    const d = argsDigest("claude", ["read <SANDBOX>/shot.png"]);
    const file = store({ mode: "replay", scripted: [], responses: [{ bin: "claude", argsDigest: d, stdout: "x", exitCode: 0 }], misses: [], used: [] });
    expect(fakeModel("claude", ["read /tmp/golden-1/shot.png"], file, ["", "/tmp/golden-1"]).stdout).toBe("x");
  });

  it("keys a sandbox temp dir by its prefix, not mkdtemp's random suffix", () => {
    const d = argsDigest("codex", ["--output-schema", "<SANDBOX>/tmp/judge-cli-XXXXXX/schema.json"]);
    const file = store({ mode: "replay", scripted: [], responses: [{ bin: "codex", argsDigest: d, stdout: "y", exitCode: 0 }], misses: [], used: [] });
    expect(fakeModel("codex", ["--output-schema", "/tmp/golden-2/tmp/judge-cli-a1B2c3/schema.json"], file, ["/tmp/golden-2"]).stdout).toBe("y");
  });

  it("fails loudly when the store has the wrong shape", () => {
    const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "golden-fm-")), "models.json");
    fs.writeFileSync(file, JSON.stringify({ mode: "replay" }));
    const r = fakeModel("claude", [], file);
    expect(r.exitCode).toBe(97);
    expect(r.stderr).toContain("models.json: scripted: Required");
  });

  it("fails loudly when the store is missing", () => {
    const r = fakeModel("claude", [], "/nonexistent/models.json");
    expect(r.exitCode).toBe(97);
    expect(r.stderr).toContain("golden-fake-model: cannot read store");
  });
});
