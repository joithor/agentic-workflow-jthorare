import path from "node:path";
import { fileURLToPath } from "node:url";

import type { RunOptions } from "../src/run.js";
import { parseSuite, type Case } from "../src/schema.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const golden = path.resolve(here, "..");

// Runner options for the test target (tests/fixtures/target.mjs) as CLI "t".
export const OPTS: Omit<RunOptions, "mode"> = {
  repo: golden,
  distDir: path.join(golden, "dist"),
  targets: { t: ["node", "{{repo}}/tests/fixtures/target.mjs"] },
};

export const GOLDEN_DIR = golden;

// One parsed case (defaults filled) running CLI "t".
export function kase(raw: Record<string, unknown>): Case {
  const r = parseSuite({ schema: "golden-suite/v1", name: "s", normalize: [], inputs: { a: "", seed: "" }, cases: [{ name: "c", run: { cli: "t" }, ...raw }] });
  if (!r.ok) throw new Error(r.error);
  return r.suite.cases[0] as Case;
}
