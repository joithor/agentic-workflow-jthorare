#!/usr/bin/env node
// Entry point the sandbox's claude/codex/cursor-agent shims exec: argv[2] is the bin name.
import { fakeModel } from "./fake-model.js";

const [, , bin = "", ...args] = process.argv;
const roots = (process.env.GOLDEN_SANDBOX_ROOTS ?? "").split(":");
const r = fakeModel(bin, args, process.env.GOLDEN_FAKE_STORE ?? "", roots);
process.stdout.write(r.stdout);
process.stderr.write(r.stderr === "" ? "" : `${r.stderr}\n`);
process.exit(r.exitCode);
