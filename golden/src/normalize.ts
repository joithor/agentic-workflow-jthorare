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
// `fired` collects the rules that changed something (record uses it to refuse
// a declared rule that never applies).
export function normalize(text: string, rules: readonly Normalizer[], ctx: NormalizeContext, fired?: Set<Normalizer>): string {
  const apply = (rule: Normalizer, f: (s: string) => string, s: string): string => {
    if (!rules.includes(rule)) return s;
    const out = f(s);
    if (out !== s) fired?.add(rule);
    return out;
  };
  const roots = [ctx.sandboxReal, ctx.sandbox].filter((r): r is string => r !== undefined);
  let out = apply("sandbox-path", (s) => roots.reduce((acc, r) => acc.replace(new RegExp(escapeRe(r), "g"), "<SANDBOX>"), s), text);
  out = apply("uuid", (s) => s.replace(UUID, "<UUID>"), out);
  out = apply("iso-timestamp", (s) => s.replace(ISO, "<TS>"), out);
  out = apply("epoch-ms", (s) => s.replace(EPOCH_MS, "<EPOCH_MS>"), out);
  return apply("duration-ms", (s) => s.replace(DURATION, "$1<MS>"), out);
}
