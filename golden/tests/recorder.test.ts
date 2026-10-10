import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { recordCall, recordingsDir, scrubFields, scrubInputs, sindriScrub, type Scrub } from "../src/recorder.js";
import { GOLDEN_DIR } from "./helpers.js";

const now = (): Date => new Date("2026-10-10T00:00:00.000Z");
// A stand-in scrubber with the same filter contract as `sindri scrub`.
const upper: Scrub = (t) => t.replace(/SECRET[0-9]+/g, "[REDACTED:test]");
const tmp = (): string => fs.mkdtempSync(path.join(os.tmpdir(), "golden-rec-"));

describe("recordingsDir", () => {
  it("lives under AW_STATE_DIR, else under ~/.agentic-workflow", () => {
    expect(recordingsDir({ AW_STATE_DIR: "/s" })).toBe("/s/golden/recordings");
    expect(recordingsDir({ AW_STATE_DIR: "" })).toBe(path.join(os.homedir(), ".agentic-workflow", "golden", "recordings"));
  });
});

describe("scrubFields", () => {
  it("scrubs every field in one call and splits them back", () => {
    expect(scrubFields(["a SECRET1", "b", "SECRET22 c"], upper)).toEqual(["a [REDACTED:test]", "b", "[REDACTED:test] c"]);
  });

  it("returns null when the scrubber fails or a match swallows a separator", () => {
    expect(scrubFields(["a"], () => null)).toBeNull();
    expect(scrubFields(["a", "b"], () => "joined")).toBeNull();
  });
});

// A token right after a JSON-escaped newline: "\\n" + token, with no word boundary in the raw text.
const P1 = "gh";
const P2 = "p_";
const TOKEN = `${P1}${P2}${"Ab3dE6gH9j".repeat(4)}`;
const JSON_STDIN = JSON.stringify({ prompt: `line one\n${TOKEN}`, n: 1, list: [`x ${TOKEN}`, null, true], [`key ${TOKEN}`]: "v" });
const word: Scrub = (t) => t.replace(/\bghp_[A-Za-z0-9]{30,}/g, "[REDACTED:github-token]");

describe("scrubInputs", () => {
  it("scrubs a JSON field by its decoded leaves and keys, so an escape can't hide a token", () => {
    expect(word(JSON_STDIN)).toContain(TOKEN);
    const [out] = scrubInputs([JSON_STDIN], word) as string[];
    expect(out).not.toContain(P1 + P2);
    expect(JSON.parse(out as string)).toEqual({ prompt: "line one\n[REDACTED:github-token]", n: 1, list: ["x [REDACTED:github-token]", null, true], "key [REDACTED:github-token]": "v" });
  });

  it("keeps a JSON field's original bytes when nothing in it changed, and scrubs plain fields as text", () => {
    const pretty = '{ "a": "b" }';
    expect(scrubInputs([pretty, "plain " + TOKEN, "42"], word)).toEqual([pretty, "plain [REDACTED:github-token]", "42"]);
  });

  it("returns null when the scrubber fails", () => {
    expect(scrubInputs([JSON_STDIN], () => null)).toBeNull();
  });

  const sindri = path.resolve(GOLDEN_DIR, "..", "sindri", "dist", "cli.js");
  it.runIf(fs.existsSync(sindri))("redacts a newline-preceded token in JSON stdin with the real sindri scrub", () => {
    const state = tmp();
    const r = recordCall({ name: "real", argv: ["hook"], env: { AW_STATE_DIR: state }, stdin: JSON_STDIN, allowlist: [] }, sindriScrub([process.execPath, sindri], { ...process.env, AW_STATE_DIR: state }), now);
    expect(r.recorded).toBe(true);
    const line = fs.readFileSync(r.file as string, "utf8");
    expect(line).not.toContain(P1 + P2);
    expect((JSON.parse(JSON.parse(line).stdin) as { prompt: string }).prompt).toBe("line one\n[REDACTED:github-token]");
  });
});

describe("recordCall", () => {
  it("appends one scrubbed inputs-only JSON line: argv, allowlisted env, stdin", () => {
    const state = tmp();
    const r = recordCall(
      { name: "send-gate", argv: ["judge", "wake-gate", "SECRET9"], env: { AW_STATE_DIR: state, AW_SESSION_ID: "s1", HOME: "/h", OTHER: "SECRET3" }, stdin: '{"text":"SECRET77"}', allowlist: ["AW_SESSION_ID", "AW_PROVIDER"] },
      upper, now,
    );
    expect(r).toEqual({ recorded: true, file: path.join(state, "golden", "recordings", "send-gate.jsonl") });
    const line = fs.readFileSync(r.file as string, "utf8");
    expect(JSON.parse(line)).toEqual({ schema: "golden-call/v1", ts: "2026-10-10T00:00:00.000Z", name: "send-gate", argv: ["judge", "wake-gate", "[REDACTED:test]"], env: { AW_SESSION_ID: "s1" }, stdin: '{"text":"[REDACTED:test]"}' });
    expect(line).not.toContain("SECRET");
    expect(fs.statSync(r.file as string).mode & 0o777).toBe(0o600);
  });

  it("forces 0700/0600 on an existing loose dir and file, and refuses a symlinked file", () => {
    const state = tmp();
    const dir = path.join(state, "golden", "recordings");
    fs.mkdirSync(dir, { recursive: true, mode: 0o755 });
    fs.writeFileSync(path.join(dir, "loose.jsonl"), "", { mode: 0o644 });
    const r = recordCall({ name: "loose", argv: ["x"], env: { AW_STATE_DIR: state }, stdin: "", allowlist: [] }, upper, now);
    expect(r.recorded).toBe(true);
    expect(fs.statSync(dir).mode & 0o777).toBe(0o700);
    expect(fs.statSync(path.join(dir, "loose.jsonl")).mode & 0o777).toBe(0o600);
    fs.symlinkSync(path.join(state, "elsewhere"), path.join(dir, "link.jsonl"));
    const l = recordCall({ name: "link", argv: ["x"], env: { AW_STATE_DIR: state }, stdin: "", allowlist: [] }, upper, now);
    expect(l.recorded).toBe(false);
    expect(l.warning).toContain("refusing a symlink");
  });

  it("records nothing for an invalid name, a failed scrubber, or an unwritable dir", () => {
    const base = { argv: ["x"], env: {}, stdin: "", allowlist: [] };
    expect(recordCall({ ...base, name: "../evil" }, upper, now)).toEqual({ recorded: false, warning: 'golden record-call: invalid --name "../evil"' });
    expect(recordCall({ ...base, name: "ok" }, () => null, now).warning).toContain("scrubber unavailable");
    const file = path.join(tmp(), "f");
    fs.writeFileSync(file, "");
    const r = recordCall({ ...base, name: "ok", env: { AW_STATE_DIR: file } }, upper, now);
    expect(r.recorded).toBe(false);
    expect(r.warning).toContain("cannot write");
  });
});

describe("sindriScrub", () => {
  it("pipes the text through the scrubber CLI's stdin filter", () => {
    const dir = tmp();
    const bin = path.join(dir, "fake-sindri");
    fs.writeFileSync(bin, '#!/bin/sh\n[ "$1" = scrub ] || exit 9\nsed "s/SECRET[0-9]*/[REDACTED:x]/g"\n', { mode: 0o755 });
    expect(sindriScrub([bin], process.env)("a SECRET1")).toBe("a [REDACTED:x]");
  });

  it("returns null when the scrubber exits non-zero or doesn't exist", () => {
    const dir = tmp();
    const bin = path.join(dir, "failing");
    fs.writeFileSync(bin, "#!/bin/sh\nexit 1\n", { mode: 0o755 });
    expect(sindriScrub([bin], process.env)("x")).toBeNull();
    expect(sindriScrub([path.join(dir, "missing")], process.env)("x")).toBeNull();
  });
});
