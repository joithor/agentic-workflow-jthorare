import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import Database from "better-sqlite3";

import type { Observed } from "./compare.js";
import { readStore, writeStore } from "./fake-model.js";
import { normalize } from "./normalize.js";
import { decodeText, type Case, type ModelResponse, type Normalizer } from "./schema.js";

export type Targets = Record<string, string[]>;

export interface RunOptions {
  repo: string;
  targets: Targets;
  mode: "record" | "replay";
  // Directory holding the built fake-model-bin.js.
  distDir: string;
  timeoutMs?: number;
}

export interface Sandbox {
  root: string;
  rootReal: string;
  env: Record<string, string>;
  store: string;
  shim(cli: string): string;
}

export const MODEL_BINS = ["claude", "codex", "cursor-agent"] as const;

const q = (s: string): string => `'${s.replace(/'/g, "'\\''")}'`;

function writeExec(file: string, body: string): void {
  fs.writeFileSync(file, `#!/bin/sh\n${body}\n`, { mode: 0o755 });
}

export function substitute(s: string, vars: { repo: string; sandbox: string }): string {
  return s.replaceAll("{{repo}}", vars.repo).replaceAll("{{sandbox}}", vars.sandbox);
}

// A fresh root per case: HOME and AW_STATE_DIR inside it, nothing inherited from
// the caller's environment, and a PATH that puts the fake model CLIs and a
// keychain stub in front of the target shims and the system dirs.
export function makeSandbox(opts: RunOptions, caseEnv: Record<string, string> = {}): Sandbox {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "golden-"));
  const rootReal = fs.realpathSync(root);
  for (const d of ["home", "state", "tmp", "work", "bin/fake", "bin/shim", "bin/tools", ".golden"]) fs.mkdirSync(path.join(root, d), { recursive: true });
  const fakeBin = path.join(opts.distDir, "fake-model-bin.js");
  for (const bin of MODEL_BINS) writeExec(path.join(root, "bin/fake", bin), `exec ${q(process.execPath)} ${q(fakeBin)} ${bin} "$@"`);
  // The judge's API-key lookup must never reach the real keychain.
  writeExec(path.join(root, "bin/fake", "security"), "exit 44");
  fs.symlinkSync(process.execPath, path.join(root, "bin/tools", "node"));
  for (const [name, cmd] of Object.entries(opts.targets)) {
    writeExec(path.join(root, "bin/shim", name), `exec ${cmd.map((c) => q(substitute(c, { repo: opts.repo, sandbox: root }))).join(" ")} "$@"`);
  }
  const store = path.join(root, ".golden", "models.json");
  const env: Record<string, string> = {
    PATH: ["bin/fake", "bin/shim", "bin/tools"].map((d) => path.join(root, d)).concat(["/usr/bin", "/bin"]).join(":"),
    HOME: path.join(root, "home"),
    AW_STATE_DIR: path.join(root, "state"),
    TMPDIR: path.join(root, "tmp"),
    TZ: "UTC",
    LC_ALL: "C",
    NO_COLOR: "1",
    GOLDEN_FAKE_STORE: store,
    GOLDEN_SANDBOX_ROOTS: [rootReal, root].join(":"),
  };
  for (const [k, v] of Object.entries(caseEnv)) env[k] = substitute(v, { repo: opts.repo, sandbox: root });
  return { root, rootReal, env, store, shim: (cli) => path.join(root, "bin/shim", cli) };
}

function exec(file: string, args: string[], stdin: string, sb: Sandbox, timeoutMs: number): { stdout: string; stderr: string; exitCode: number } {
  const r = spawnSync(file, args, { input: stdin, env: sb.env, cwd: path.join(sb.root, "work"), timeout: timeoutMs, encoding: "utf8" });
  if (r.error !== undefined) return { stdout: r.stdout ?? "", stderr: `${r.stderr ?? ""}<spawn error: ${r.error.message}>`, exitCode: -1 };
  return { stdout: r.stdout, stderr: r.stderr, exitCode: r.status ?? -1 };
}

function dumpTable(dbFile: string, table: string, orderBy: string): string[] {
  if (!fs.existsSync(dbFile)) return ["<missing database>"];
  const db = new Database(dbFile, { readonly: true });
  let rows: string[];
  try {
    rows = db.prepare(`SELECT * FROM ${table} ORDER BY ${orderBy}`).all().map((row) => JSON.stringify(row));
  } catch (e) {
    rows = [`<error: ${(e as Error).message}>`];
  }
  db.close();
  return rows;
}

export function runCase(c: Case, rules: readonly Normalizer[], opts: RunOptions): { observed: Observed; models: ModelResponse[]; unused: ModelResponse[] } {
  const sb = makeSandbox(opts, c.env);
  const timeoutMs = opts.timeoutMs ?? 30_000;
  try {
    for (const [rel, content] of Object.entries(c.setup.files)) {
      const file = path.join(sb.root, rel);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, decodeText(content));
    }
    const record = opts.mode === "record";
    writeStore(sb.store, {
      mode: opts.mode,
      scripted: record ? c.models.map(({ argsDigest: _d, ...r }) => r) : [],
      responses: record ? [] : c.models,
      misses: [],
    });
    for (const step of c.setup.steps) exec("cli" in step ? sb.shim(step.cli) : path.join(opts.repo, step.script), step.args.map((a) => substitute(a, { repo: opts.repo, sandbox: sb.root })), decodeText(step.stdin), sb, timeoutMs);
    for (const sql of c.setup.sql) {
      const db = new Database(path.join(sb.root, sql.db));
      try {
        for (const s of sql.statements) db.exec(s);
      } finally {
        db.close();
      }
    }
    const file = "cli" in c.run ? sb.shim(c.run.cli) : path.join(opts.repo, c.run.script);
    const out = exec(file, c.args.map((a) => substitute(a, { repo: opts.repo, sandbox: sb.root })), decodeText(c.stdin), sb, timeoutMs);
    const n = (s: string): string => normalize(s, rules, { sandbox: sb.root, sandboxReal: sb.rootReal });
    const files: Record<string, string | null> = {};
    for (const rel of c.capture.files) {
      const p = path.join(sb.root, rel);
      files[rel] = fs.existsSync(p) ? n(fs.readFileSync(p, "utf8")) : null;
    }
    const sqlite: Record<string, string[]> = {};
    for (const cap of c.capture.sqlite) sqlite[`${cap.db}#${cap.table}`] = dumpTable(path.join(sb.root, cap.db), cap.table, cap.orderBy).map(n);
    const store = readStore(sb.store);
    return {
      observed: { stdout: n(out.stdout), stderr: n(out.stderr), exitCode: out.exitCode, files, sqlite, misses: store.misses },
      models: store.responses,
      unused: store.scripted,
    };
  } finally {
    fs.rmSync(sb.root, { recursive: true, force: true });
  }
}
