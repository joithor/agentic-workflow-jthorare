#!/usr/bin/env node
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { formatDiff, redactionDiff } from "./differential.js";
import { DEFAULT_ENV_ALLOWLIST, recordCall, sindriScrub } from "./recorder.js";
import { formatReport, loadSuite, measureStartup, recordSuite, replaySuite, saveSuite } from "./suite.js";
import { loadTargets, parseTargetOverride } from "./targets.js";

const distDir = path.dirname(fileURLToPath(import.meta.url));
const USAGE = `usage:
  golden record <suite.json>... [--repo DIR] [--targets FILE] [--target name=cmd]
  golden replay <suite.json>... [--repo DIR] [--targets FILE] [--target name=cmd]
  golden record-call --name NAME [--env VAR]... -- <command> [args...]
  golden redaction-diff <redaction.suite.json>`;

function readStdin(): string {
  try {
    return fs.readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function recordCallMain(argv: string[]): number {
  const dash = argv.indexOf("--");
  const own = dash === -1 ? argv : argv.slice(0, dash);
  const cmd = dash === -1 ? [] : argv.slice(dash + 1);
  const name = own[own.indexOf("--name") + 1] ?? "";
  if (!own.includes("--name") || cmd.length === 0) {
    process.stderr.write(`${USAGE}\n`);
    return 2;
  }
  const extra = own.flatMap((a, i) => (a === "--env" && own[i + 1] !== undefined ? [own[i + 1] as string] : []));
  const stdin = readStdin();
  const r = recordCall({ name, argv: cmd, env: process.env, stdin, allowlist: [...DEFAULT_ENV_ALLOWLIST, ...extra] }, sindriScrub(process.env.GOLDEN_SCRUBBER ?? "sindri", process.env), () => new Date());
  if (r.warning !== undefined) process.stderr.write(`${r.warning}\n`);
  const run = spawnSync(cmd[0] as string, cmd.slice(1), { input: stdin, stdio: ["pipe", "inherit", "inherit"] });
  if (run.error !== undefined) {
    process.stderr.write(`golden record-call: ${run.error.message}\n`);
    return 127;
  }
  return run.status ?? 1;
}

function suiteMain(cmd: "record" | "replay", argv: string[]): number {
  const files: string[] = [];
  let repo = path.resolve(distDir, "..", "..");
  let targetsFile: string | undefined;
  const overrides: string[] = [];
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i] as string;
    if (a === "--repo") repo = path.resolve(argv[++i] ?? "");
    else if (a === "--targets") targetsFile = argv[++i];
    else if (a === "--target") overrides.push(argv[++i] ?? "");
    else files.push(a);
  }
  if (files.length === 0) {
    process.stderr.write(`${USAGE}\n`);
    return 2;
  }
  let targets;
  let suites;
  try {
    targets = { ...loadTargets(targetsFile ?? path.join(repo, "goldens", "targets.ts.json")), ...Object.fromEntries(overrides.map(parseTargetOverride)) };
    suites = files.map((f) => ({ file: f, suite: loadSuite(f) }));
  } catch (e) {
    process.stderr.write(`golden: ${(e as Error).message}\n`);
    return 2;
  }
  const opts = { repo, targets, distDir };
  let ok = true;
  for (const { file, suite } of suites) {
    if (cmd === "record") {
      try {
        saveSuite(file, recordSuite(suite, opts));
      } catch (e) {
        process.stderr.write(`golden: ${file}: ${(e as Error).message}\n`);
        ok = false;
        continue;
      }
      const s = measureStartup(suite, { ...opts, mode: "record" });
      process.stdout.write(`recorded ${suite.cases.length} case(s) -> ${file}${s === undefined ? "" : ` (startup ${s.cli}: median ${s.medianMs} ms, budget ${s.budgetMs} ms)`}\n`);
    } else {
      const r = replaySuite(suite, opts);
      process.stdout.write(`${formatReport(r)}\n`);
      ok &&= r.ok;
    }
  }
  return ok ? 0 : 1;
}

const [, , cmd, ...rest] = process.argv;
let code: number;
if (cmd === "record" || cmd === "replay") code = suiteMain(cmd, rest);
else if (cmd === "record-call") code = recordCallMain(rest);
else if (cmd === "redaction-diff" && rest[0] !== undefined) {
  try {
    process.stdout.write(formatDiff(redactionDiff(loadSuite(rest[0]))));
    code = 0;
  } catch (e) {
    process.stderr.write(`golden: ${(e as Error).message}\n`);
    code = 2;
  }
}
else {
  process.stderr.write(`${USAGE}\n`);
  code = 2;
}
process.exit(code);
