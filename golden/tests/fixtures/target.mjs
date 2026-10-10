#!/usr/bin/env node
// Test target for the runner: exercises every captured channel.
import { execFileSync } from "node:child_process";
import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

import Database from "better-sqlite3";

const [mode = "echo", ...rest] = process.argv.slice(2);
const stdin = fs.readFileSync(0, "utf8");
const state = process.env.AW_STATE_DIR;
if (mode === "echo") {
  process.stdout.write(`args=${rest.join(",")} stdin=${stdin} id=${crypto.randomUUID()} at=${new Date().toISOString()} home=${process.env.HOME}\n`);
  process.stderr.write(`env=${process.env.TEST_VAR ?? ""}\n`);
  fs.mkdirSync(path.join(state, "t"), { recursive: true });
  fs.writeFileSync(path.join(state, "t", "out.json"), JSON.stringify({ elapsed_ms: Date.now() % 1000, seen: rest }));
  const db = new Database(path.join(state, "t", "db.sqlite"));
  db.exec("CREATE TABLE IF NOT EXISTS rows (id TEXT, n INTEGER)");
  db.prepare("INSERT INTO rows VALUES (?, ?)").run(crypto.randomUUID(), rest.length);
  db.close();
  process.exit(rest.includes("fail") ? 3 : 0);
}
if (mode === "model") {
  const out = execFileSync("claude", ["-p", stdin], { encoding: "utf8" });
  process.stdout.write(out);
}
if (mode === "model-tolerant") {
  try {
    process.stdout.write(execFileSync("claude", ["-p", stdin], { encoding: "utf8", stdio: ["pipe", "pipe", "ignore"] }));
  } catch {
    process.stdout.write("model failed\n");
  }
}
if (mode === "network") {
  const net = await import("node:net");
  await new Promise((resolve) => {
    net.connect(443, "1.1.1.1").on("connect", () => resolve(process.stdout.write("network reachable\n"))).on("error", (e) => resolve(process.stdout.write(`network blocked ${e.code}\n`)));
  });
  process.exit(0);
}
if (mode === "keychain-real") {
  try {
    execFileSync("/usr/bin/security", ["list-keychains"], { stdio: "ignore" });
    process.stdout.write("keychain reachable\n");
  } catch (e) {
    process.stdout.write(`keychain blocked ${e.status}\n`);
  }
}
if (mode === "keychain") {
  try {
    execFileSync("security", ["find-generic-password"], { stdio: "ignore" });
  } catch (e) {
    process.stdout.write(`security exit ${e.status}\n`);
  }
}
