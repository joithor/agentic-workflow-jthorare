import { describe, expect, it } from "vitest";

import { formatDiff, redactionDiff } from "../src/differential.js";
import type { Suite } from "../src/schema.js";

const exp = (stdout: string) => ({ stdout: { base64: Buffer.from(stdout).toString("base64") }, stderr: "", exitCode: 0, files: {}, sqlite: {} });
const kase = (name: string, stdout?: string) => ({
  name, run: { cli: "x" }, args: [], env: {}, stdin: "", setup: { files: {}, steps: [], sql: [] }, capture: { files: [], sqlite: [] }, models: [],
  ...(stdout === undefined ? {} : { expect: exp(stdout) }),
});

describe("redactionDiff", () => {
  it("pairs judge and sindri cases by id and compares placeholders as one token", () => {
    const suite: Suite = {
      schema: "golden-suite/v1", name: "r", normalize: [],
      cases: [
        kase("judge/a", "x [REDACTED] y"), kase("sindri/a", "x [REDACTED:github-token] y"),
        kase("judge/b", "x secretish"), kase("sindri/b", "x [REDACTED:ssn]"),
        kase("judge/only-one", "z"), kase("sindri/unrecorded"), kase("other"),
      ],
    };
    const rows = redactionDiff(suite);
    expect(rows).toEqual([
      { id: "a", judgeHits: 1, sindriHits: 1, same: true },
      { id: "b", judgeHits: 0, sindriHits: 1, same: false },
    ]);
    const md = formatDiff(rows);
    expect(md).toContain("2 corpus inputs; 1 redact differently");
    expect(md).toContain("| b | 0 | 1 | **no** |");
    expect(md).not.toContain("secretish");
  });
});
