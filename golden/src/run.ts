import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

import Database from "better-sqlite3";

import type { Observed } from "./compare.js";
import { readStore, writeStore } from "./fake-model.js";
import { normalize } from "./normalize.js";
import { decodeText, type Case, type Inputs, type ModelResponse, type Normalizer } from "./schema.js";

export type Targets = Record<string, string[]>;

export interface RunOptions {
  repo: string;
  targets: Targets;
  mode: "record" | "replay";
  // Directory holding the built fake-model-bin.js.
  distDir: string;
  timeoutMs?: number;
  // OS-level tripwire (darwin sandbox-exec): no outbound network, no keychain.
  // Defaults to on wherever sandbox-exec exists.
  osSandbox?: boolean;
}

export interface Sandbox {
  root: string;
  rootReal: string;
  env: Record<string, string>;
  store: string;
  shim(cli: string): string;
  inside(rel: string): string;
}

export const MODEL_BINS = ["claude", "codex", "cursor-agent"] as const;
export const SANDBOX_EXEC = "/usr/bin/sandbox-exec";

const q = (s: string): string => `'${s.replace(/'/g, "'\\''")}'`;

function writeExec(file: string, body: string): void {
  fs.writeFileSync(file, `#!/bin/sh\n${body}\n`, { mode: 0o755 });
}

export function substitute(s: string, vars: { repo: string; sandbox: string }): string {
  return s.replaceAll("{{repo}}", vars.repo).replaceAll("{{sandbox}}", vars.sandbox);
}

// The schema already rejects absolute paths and "..": this is the second line.
export function contained(base: string, rel: string): string {
  const p = path.resolve(base, rel);
  if (p !== base && !p.startsWith(`${base}${path.sep}`)) throw new Error(`path escapes its root: ${rel}`);
  return p;
}

// Denies outbound network (except loopback) and the keychain to everything a
// case runs, so a non-TS build that bypasses the PATH stubs (Security.framework,
// a hard-coded CLI path) fails instead of reaching a real service.
export function darwinProfile(realHome: string): string {
  return [
    "(version 1)",
    "(allow default)",
    '(deny network-outbound (remote ip "*:*"))',
    '(allow network-outbound (remote ip "localhost:*"))',
    '(deny mach-lookup (global-name "com.apple.SecurityServer") (global-name "com.apple.securityd"))',
    `(deny file-read* (subpath ${JSON.stringify(path.join(realHome, "Library", "Keychains"))}))`,
  ].join("\n");
}

export function useOsSandbox(opts: Pick<RunOptions, "osSandbox">): boolean {
  return opts.osSandbox ?? fs.existsSync(SANDBOX_EXEC);
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
  const env: Record<string, string> = {};
  for (const [k, v] of Object.entries(caseEnv)) env[k] = substitute(v, { repo: opts.repo, sandbox: root });
  // Sandbox keys last, so nothing a case sets can override them (the schema rejects them too).
  Object.assign(env, {
    PATH: ["bin/fake", "bin/shim", "bin/tools"].map((d) => path.join(root, d)).concat(["/usr/bin", "/bin"]).join(":"),
    HOME: path.join(root, "home"),
    AW_STATE_DIR: path.join(root, "state"),
    TMPDIR: path.join(root, "tmp"),
    TZ: "UTC",
    LC_ALL: "C",
    NO_COLOR: "1",
    GOLDEN_FAKE_STORE: store,
    GOLDEN_SANDBOX_ROOTS: [rootReal, root].join(":"),
  });
  return { root, rootReal, env, store, shim: (cli) => path.join(root, "bin/shim", cli), inside: (rel) => contained(root, rel) };
}

export interface ExecResult {
  stdout: string;
  stderr: string;
  exitCode: number;
}

export function exec(file: string, args: string[], stdin: string, sb: Sandbox, o: { timeoutMs: number; osSandbox: boolean }): ExecResult {
  const [cmd, argv] = o.osSandbox ? [SANDBOX_EXEC, ["-p", darwinProfile(os.homedir()), file, ...args]] : [file, args];
  const r = spawnSync(cmd, argv, { input: stdin, env: sb.env, cwd: path.join(sb.root, "work"), timeout: o.timeoutMs, encoding: "utf8" });
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

export interface CaseRun {
  observed: Observed;
  // record: the responses keyed by digest; scripted ones never called.
  models: ModelResponse[];
  unused: ModelResponse[];
}

// Runs one case. Throws on a broken setup (a step that exits non-zero, SQL
// that fails); the suite runner turns that into a failed case.
export function runCase(c: Case, suite: { normalize: readonly Normalizer[]; inputs?: Inputs }, opts: RunOptions, fired?: Set<Normalizer>): CaseRun {
  const sb = makeSandbox(opts, c.env);
  const inputs = suite.inputs ?? {};
  const xo = { timeoutMs: opts.timeoutMs ?? 30_000, osSandbox: useOsSandbox(opts) };
  const vars = { repo: opts.repo, sandbox: sb.root };
  try {
    for (const [rel, content] of Object.entries(c.setup.files)) {
      const file = sb.inside(rel);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.writeFileSync(file, decodeText(content, inputs));
    }
    const record = opts.mode === "record";
    writeStore(sb.store, {
      mode: opts.mode,
      scripted: record ? c.models.map(({ argsDigest: _d, ...r }) => r) : [],
      responses: record ? [] : c.models,
      misses: [],
      used: [],
    });
    c.setup.steps.forEach((step, i) => {
      const file = "cli" in step ? sb.shim(step.cli) : contained(opts.repo, step.script);
      const r = exec(file, step.args.map((a) => substitute(a, vars)), decodeText(step.stdin, inputs), sb, xo);
      if (r.exitCode !== 0) throw new Error(`setup step ${i + 1} (${"cli" in step ? step.cli : step.script}) exited ${r.exitCode}: ${r.stderr.trim()}`);
    });
    for (const sql of c.setup.sql) {
      const file = sb.inside(sql.db);
      fs.mkdirSync(path.dirname(file), { recursive: true });
      const db = new Database(file);
      try {
        for (const s of sql.statements) db.exec(s);
      } finally {
        db.close();
      }
    }
    const file = "cli" in c.run ? sb.shim(c.run.cli) : contained(opts.repo, c.run.script);
    const out = exec(file, c.args.map((a) => substitute(a, vars)), decodeText(c.stdin, inputs), sb, xo);
    const n = (s: string): string => normalize(s, suite.normalize, { sandbox: sb.root, sandboxReal: sb.rootReal }, fired);
    const files: Record<string, string | null> = {};
    for (const rel of c.capture.files) {
      const p = sb.inside(rel);
      files[rel] = fs.existsSync(p) ? n(fs.readFileSync(p, "utf8")) : null;
    }
    const sqlite: Record<string, string[]> = {};
    for (const cap of c.capture.sqlite) sqlite[`${cap.db}#${cap.table}`] = dumpTable(sb.inside(cap.db), cap.table, cap.orderBy).map(n);
    const store = readStore(sb.store);
    const used = new Set(store.used);
    const unusedRecorded = record ? [] : store.responses.map((r) => `${r.bin} ${r.argsDigest ?? "?"}`).filter((k) => !used.has(k));
    return {
      observed: { stdout: n(out.stdout), stderr: n(out.stderr), exitCode: out.exitCode, files, sqlite, misses: store.misses, unusedRecorded },
      models: store.responses,
      unused: store.scripted,
    };
  } finally {
    fs.rmSync(sb.root, { recursive: true, force: true });
  }
}
