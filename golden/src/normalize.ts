import type { Normalizer } from "./schema.js";

export interface NormalizeContext {
  sandbox: string;
  // The same root after symlink resolution (macOS /tmp -> /private/tmp).
  sandboxReal?: string;
}

const escapeRe = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

const UUID = /\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b/gi;
const ISO = /\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,9})?(?:Z|[+-]\d{2}:\d{2})/g;
const EPOCH_MS = /\b1[0-9]{12}\b/g;
const DURATION = /("[A-Za-z0-9]*(?:_ms|Ms)"\s*:\s*)\d+(?:\.\d+)?/g;

// Rules apply in a fixed order, whatever order the suite lists them in:
// the sandbox path first, so a uuid or timestamp inside it can't split it.
export function normalize(text: string, rules: readonly Normalizer[], ctx: NormalizeContext): string {
  let out = text;
  if (rules.includes("sandbox-path")) {
    for (const root of [ctx.sandboxReal, ctx.sandbox]) {
      if (root !== undefined) out = out.replace(new RegExp(escapeRe(root), "g"), "<SANDBOX>");
    }
  }
  if (rules.includes("uuid")) out = out.replace(UUID, "<UUID>");
  if (rules.includes("iso-timestamp")) out = out.replace(ISO, "<TS>");
  if (rules.includes("epoch-ms")) out = out.replace(EPOCH_MS, "<EPOCH_MS>");
  if (rules.includes("duration-ms")) out = out.replace(DURATION, "$1<MS>");
  return out;
}
