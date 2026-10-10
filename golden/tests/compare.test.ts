import { describe, expect, it } from "vitest";

import { compare, lineDiff } from "../src/compare.js";
import type { Observed } from "../src/compare.js";

const base: Observed = { stdout: "a\nb\n", stderr: "", exitCode: 0, files: { "state/x": "1" }, sqlite: { t: ['{"a":1}'] }, misses: [], unusedRecorded: [] };

describe("compare", () => {
  it("returns no mismatches when every captured field is identical", () => {
    const { misses: _m, unusedRecorded: _u, ...exp } = base;
    expect(compare(exp, base)).toEqual([]);
  });

  it("names each mismatching field with a line diff", () => {
    const actual: Observed = { ...base, stdout: "a\nc\n", exitCode: 2, files: { "state/x": null }, sqlite: { t: ['{"a":2}'] }, misses: ["claude 1234"] };
    const { misses: _m, unusedRecorded: _u, ...exp } = base;
    const m = compare(exp, actual);
    expect(m.map((x) => x.field)).toEqual(["stdout", "exitCode", "files[state/x]", "sqlite[t]", "model calls"]);
    expect(m[0].diff).toBe("line 2:\n  - b\n  + c");
    expect(m[1].diff).toBe("- 0\n+ 2");
    expect(m[2].diff).toBe("- 1\n+ <missing>");
  });

  it("fails a recorded model call the build never made", () => {
    const { misses: _m, unusedRecorded: _u, ...exp } = base;
    const m = compare(exp, { ...base, unusedRecorded: ["claude 1234"] });
    expect(m).toEqual([{ field: "model calls", diff: "- claude 1234\n+ <never called>" }]);
  });

  it("reports a table or file the expectation doesn't name as unexpected", () => {
    const { misses: _m, unusedRecorded: _u, ...exp } = base;
    const m = compare({ ...exp, files: {}, sqlite: {} }, base);
    expect(m.map((x) => x.field)).toEqual(["files[state/x]", "sqlite[t]"]);
    expect(m[0].diff).toBe("- <missing>\n+ 1");
  });

  it("compares a base64-stored expectation by its decoded text", () => {
    const { misses: _m, unusedRecorded: _u, ...exp } = base;
    expect(compare({ ...exp, stdout: { base64: Buffer.from("a\nb\n").toString("base64") } }, base)).toEqual([]);
    expect(compare({ ...exp, stdout: { input: "i" } }, base, { i: "a\nb\n" })).toEqual([]);
  });
});

describe("lineDiff", () => {
  it("shows every differing line and the extra lines of the longer side", () => {
    expect(lineDiff("a\nb", "a\nB\nc")).toBe("line 2:\n  - b\n  + B\nline 3:\n  - <missing>\n  + c");
  });

  it("shows a byte-level difference such as a trailing newline", () => {
    expect(lineDiff("a\n", "a")).toBe("line 2:\n  - \n  + <missing>");
  });
});
