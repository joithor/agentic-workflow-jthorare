import { z } from "zod";

// Named normalization rules a suite declares up front (spec §4c: outputs are
// compared after a declared normalization, never by loose matching).
export const NORMALIZERS = ["sandbox-path", "uuid", "iso-timestamp", "epoch-ms", "duration-ms"] as const;
export type Normalizer = (typeof NORMALIZERS)[number];

// Inputs that would trip a secret scanner (the redaction corpus) are stored base64.
const Text = z.union([z.string(), z.object({ base64: z.string() }).strict()]);
export type Text = z.infer<typeof Text>;

const Run = z.union([z.object({ cli: z.string().min(1) }).strict(), z.object({ script: z.string().min(1) }).strict()]);

// A setup step runs a target CLI or a repo script (a hook) before the case.
const Step = z.union([
  z.object({ cli: z.string().min(1), args: z.array(z.string()).default([]), stdin: Text.default("") }).strict(),
  z.object({ script: z.string().min(1), args: z.array(z.string()).default([]), stdin: Text.default("") }).strict(),
]);

// Rows of one table, in the declared ORDER BY, serialized one JSON object per row.
const SqliteCapture = z.object({ db: z.string().min(1), table: z.string().regex(/^[a-z_]+$/), orderBy: z.string().regex(/^[a-z_, ]+$/) }).strict();

const ModelResponse = z.object({
  bin: z.string().min(1),
  argsDigest: z.string().optional(),
  stdout: z.string(),
  exitCode: z.number().int().default(0),
}).strict();

const Expect = z.object({
  stdout: z.string(),
  stderr: z.string(),
  exitCode: z.number().int(),
  files: z.record(z.string(), z.string().nullable()),
  sqlite: z.record(z.string(), z.array(z.string())),
}).strict();

const Case = z.object({
  name: z.string().min(1),
  run: Run,
  args: z.array(z.string()).default([]),
  env: z.record(z.string(), z.string()).default({}),
  stdin: Text.default(""),
  setup: z.object({
    files: z.record(z.string(), Text).default({}),
    steps: z.array(Step).default([]),
    sql: z.array(z.object({ db: z.string().min(1), statements: z.array(z.string()) }).strict()).default([]),
  }).strict().default({}),
  capture: z.object({ files: z.array(z.string()).default([]), sqlite: z.array(SqliteCapture).default([]) }).strict().default({}),
  models: z.array(ModelResponse).default([]),
  expect: Expect.optional(),
}).strict();

const Suite = z.object({
  schema: z.literal("golden-suite/v1"),
  name: z.string().min(1),
  description: z.string().optional(),
  normalize: z.array(z.enum(NORMALIZERS)),
  startupBudget: z.object({ cli: z.string().min(1), args: z.array(z.string()), runs: z.number().int().min(1), medianMsMax: z.number().positive() }).strict().optional(),
  cases: z.array(Case),
}).strict();

export type Suite = z.infer<typeof Suite>;
export type Case = z.infer<typeof Case>;
export type Expectation = z.infer<typeof Expect>;
export type ModelResponse = z.infer<typeof ModelResponse>;

export function parseSuite(raw: unknown): { ok: true; suite: Suite } | { ok: false; error: string } {
  const r = Suite.safeParse(raw);
  if (!r.success) return { ok: false, error: r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ") };
  const seen = new Set<string>();
  for (const c of r.data.cases) {
    if (seen.has(c.name)) return { ok: false, error: `duplicate case name: ${c.name}` };
    seen.add(c.name);
  }
  return { ok: true, suite: r.data };
}

export function decodeText(t: Text): string {
  return typeof t === "string" ? t : Buffer.from(t.base64, "base64").toString("utf8");
}
