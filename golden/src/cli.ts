#!/usr/bin/env node
// Wiring only: argument parsing lives in args.ts, everything else in the modules it calls.
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { parseRecordCallArgs, parseSuiteArgs, USAGE } from "./args.js";
import { formatDiff, redactionDiff } from "./differential.js";
import { recordCall, sindriScrub } from "./recorder.js";
import { substitute, useOsSandbox, type Targets } from "./run.js";
import { formatReport, formatStartup, loadSuite, measureStartup, recordSuite, replaySuite, saveSuite } from "./suite.js";
import type { Suite } from "./schema.js";
import { loadTargets, parseTargetOverride } from "./targets.js";

const distDir = path.dirname(fileURLToPath(import.meta.url));

function fail(message: string, code = 2): number {
  process.stderr.write(`${message}\n`);
  return code;
}

function recordCallMain(argv: string[]): number {
  const a = parseRecordCallArgs(argv);
  if ("error" in a) return fail(`golden: ${a.error}\n${USAGE}`);
  let stdin = "";
  try {
    stdin = fs.readFileSync(0, "utf8");
  } catch {
    /* no stdin */
  }
  const r = recordCall({ name: a.name, argv: a.command, env: process.env, stdin, allowlist: a.allowlist }, sindriScrub([process.env.GOLDEN_SCRUBBER ?? "sindri"], process.env), () => new Date());
  if (r.warning !== undefined) process.stderr.write(`${r.warning}\n`);
  const run = spawnSync(a.command[0] as string, a.command.slice(1), { input: stdin, stdio: ["pipe", "inherit", "inherit"] });
  if (run.error !== undefined) return fail(`golden record-call: ${run.error.message}`, 127);
  return run.status ?? 1;
}

function suiteMain(cmd: "record" | "replay", argv: string[]): number {
  const a = parseSuiteArgs(argv, path.resolve(distDir, "..", ".."));
  if ("error" in a) return fail(`golden: ${a.error}\n${USAGE}`);
  let targets: Targets;
  let suites: { file: string; suite: Suite }[];
  try {
    targets = { ...loadTargets(a.targetsFile), ...Object.fromEntries(a.overrides.map(parseTargetOverride)) };
    suites = a.files.map((f) => ({ file: f, suite: loadSuite(f) }));
  } catch (e) {
    return fail(`golden: ${(e as Error).message}`);
  }
  const opts = { repo: a.repo, targets, distDir, osSandbox: a.osSandbox };
  process.stderr.write(`golden: isolation = ${useOsSandbox(opts) ? "sandbox-exec (no network, no keychain) + PATH stubs" : "PATH stubs only"}\n`);
  // Record stores an output base64 only when the scrubber finds a secret shape in it.
  const scrubCmd = targets.sindri?.map((c) => substitute(c, { repo: a.repo, sandbox: "" }));
  const scrub = scrubCmd === undefined ? undefined : sindriScrub(scrubCmd, process.env);
  const secretShaped = scrub === undefined ? undefined : (t: string): boolean => scrub(t) !== t;
  let ok = true;
  for (const { file, suite } of suites) {
    if (cmd === "record") {
      try {
        saveSuite(file, recordSuite(suite, opts, { secretShaped }));
      } catch (e) {
        ok = false;
        process.stderr.write(`golden: ${file}:\n${(e as Error).message}\n`);
        continue;
      }
      const budgets = suite.startupBudgets.map((b) => formatStartup(measureStartup(b, suite, { ...opts, mode: "record" })));
      process.stdout.write(`recorded ${suite.cases.length} case(s) -> ${file}${budgets.map((b) => `\n  ${b}`).join("")}\n`);
    } else {
      const r = replaySuite(suite, opts);
      process.stdout.write(`${formatReport(r)}\n`);
      ok &&= r.ok;
    }
  }
  return ok ? 0 : 1;
}

function redactionDiffMain(argv: string[]): number {
  if (argv[0] === undefined) return fail(USAGE);
  try {
    process.stdout.write(formatDiff(redactionDiff(loadSuite(argv[0]))));
    return 0;
  } catch (e) {
    return fail(`golden: ${(e as Error).message}`);
  }
}

const [, , cmd, ...rest] = process.argv;
const code =
  cmd === "record" || cmd === "replay" ? suiteMain(cmd, rest)
  : cmd === "record-call" ? recordCallMain(rest)
  : cmd === "redaction-diff" ? redactionDiffMain(rest)
  : fail(USAGE);
process.exit(code);
