import fs from "node:fs";
import path from "node:path";

import { describe, expect, it } from "vitest";

import { formatDiff, redactionDiff } from "../src/differential.js";
import { loadSuite } from "../src/suite.js";
import { GOLDEN_DIR } from "./helpers.js";

const goldens = path.resolve(GOLDEN_DIR, "..", "goldens");

describe("committed goldens", () => {
  it("every suite parses", () => {
    const suites = fs.readdirSync(goldens, { recursive: true }).map(String).filter((f) => f.endsWith(".suite.json"));
    expect(suites.length).toBeGreaterThanOrEqual(3);
    for (const f of suites) expect(() => loadSuite(path.join(goldens, f))).not.toThrow();
  });

  it("redaction/DIFFERENCES.md matches a fresh redaction-diff of the committed suite", () => {
    const fresh = formatDiff(redactionDiff(loadSuite(path.join(goldens, "redaction", "redaction.suite.json"))));
    expect(fs.readFileSync(path.join(goldens, "redaction", "DIFFERENCES.md"), "utf8")).toBe(fresh);
  });
});
