import type { Expectation } from "./schema.js";

// What one run produced, already normalized: the same shape as an expectation.
export type Observed = Expectation;

export interface Mismatch {
  field: string;
  diff: string;
}

const show = (v: string | null | undefined): string => (v === null || v === undefined ? "<missing>" : v);

// Positional line diff: every differing line with its 1-based number. Exact,
// so a lone trailing newline shows up as an extra empty line.
export function lineDiff(expected: string, actual: string): string {
  const e = expected.split("\n");
  const a = actual.split("\n");
  const out: string[] = [];
  for (let i = 0; i < Math.max(e.length, a.length); i++) {
    if (e[i] !== a[i]) out.push(`line ${i + 1}:\n  - ${show(e[i])}\n  + ${show(a[i])}`);
  }
  return out.join("\n");
}

function keyed(field: string, expected: Record<string, string | null>, actual: Record<string, string | null>): Mismatch[] {
  const keys = [...new Set([...Object.keys(expected), ...Object.keys(actual)])].sort();
  return keys
    .filter((k) => expected[k] !== actual[k])
    .map((k) => ({ field: `${field}[${k}]`, diff: `- ${show(expected[k])}\n+ ${show(actual[k])}` }));
}

const joinRows = (r: Record<string, string[]>): Record<string, string> => Object.fromEntries(Object.entries(r).map(([k, v]) => [k, v.join("\n")]));

export function compare(expected: Expectation, actual: Observed): Mismatch[] {
  const out: Mismatch[] = [];
  for (const f of ["stdout", "stderr"] as const) {
    if (expected[f] !== actual[f]) out.push({ field: f, diff: lineDiff(expected[f], actual[f]) });
  }
  if (expected.exitCode !== actual.exitCode) out.push({ field: "exitCode", diff: `- ${expected.exitCode}\n+ ${actual.exitCode}` });
  out.push(...keyed("files", expected.files, actual.files));
  const e = joinRows(expected.sqlite);
  const a = joinRows(actual.sqlite);
  for (const t of [...new Set([...Object.keys(e), ...Object.keys(a)])].sort()) {
    if (e[t] !== a[t]) out.push({ field: `sqlite[${t}]`, diff: e[t] !== undefined && a[t] !== undefined ? lineDiff(e[t], a[t]) : `- ${show(e[t])}\n+ ${show(a[t])}` });
  }
  const em = expected.models.misses.join("\n");
  const am = actual.models.misses.join("\n");
  if (em !== am) out.push({ field: "models.misses", diff: `- ${em === "" ? "<none>" : em}\n+ ${am === "" ? "<none>" : am}` });
  return out;
}
