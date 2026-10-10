#!/usr/bin/env node
// Thin adapter: judge's redaction has no CLI of its own, so this runs judge's
// built redactSecrets (judge/dist/redact.js — imported, never copied) as a
// stdin -> stdout filter for the Redaction goldens. GOLDEN_JUDGE_DIST overrides
// where the build lives.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const dist = process.env.GOLDEN_JUDGE_DIST ?? path.resolve(here, "..", "..", "judge", "dist");
const mod = (await import(pathToFileURL(path.join(dist, "redact.js")).href)) as { redactSecrets: (t: string) => string };
process.stdout.write(mod.redactSecrets(fs.readFileSync(0, "utf8")));
