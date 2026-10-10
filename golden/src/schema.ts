import { z } from "zod";

// Named normalization rules a suite declares up front (spec §4c: outputs are
// compared after a declared normalization, never by loose matching). Record
// refuses a declared rule that never fires, so each suite keeps the minimum.
export const NORMALIZERS = ["sandbox-path", "uuid", "iso-timestamp", "epoch-ms", "duration-ms"] as const;
export type Normalizer = (typeof NORMALIZERS)[number];

// Text: a plain string, or a recipe — base64 (secret shapes), a reference to a
// suite-level input (a corpus shared by several cases), a repeat (long inputs)
// or a concatenation of these.
export type Text = string | { base64: string } | { input: string } | { repeat: string; times: number } | { concat: Text[] };
const TextSchema: z.ZodType<Text> = z.lazy(() =>
  z.union([
    z.string(),
    z.object({ base64: z.string() }).strict(),
    z.object({ input: z.string().min(1) }).strict(),
    z.object({ repeat: z.string().min(1), times: z.number().int().min(1).max(1_000_000) }).strict(),
    z.object({ concat: z.array(TextSchema) }).strict(),
  ]),
);

// Every path a suite names stays inside the sandbox (or, for scripts, the
// repo): relative, no "..", no backslashes. Suites are agent-authored and a
// recording is committed to a public repo.
const RelPath = z.string().min(1).refine((p) => !p.startsWith("/") && !p.includes("\\") && !p.split("/").includes(".."), {
  message: "must be a relative path without '..'",
});

// The sandbox owns these; a case can't point them at the live install.
export const RESERVED_ENV = ["HOME", "PATH", "AW_STATE_DIR", "TMPDIR", "TZ", "LC_ALL", "NO_COLOR"] as const;
const EnvKey = z.string().regex(/^[A-Za-z_][A-Za-z0-9_]*$/).refine((k) => !(RESERVED_ENV as readonly string[]).includes(k) && !k.startsWith("GOLDEN_"), {
  message: `is reserved by the sandbox (${RESERVED_ENV.join(", ")}, GOLDEN_*)`,
});

const Run = z.union([z.object({ cli: z.string().min(1) }).strict(), z.object({ script: RelPath }).strict()]);

// A setup step runs a target CLI or a repo script (a hook) before the case; it must exit 0.
const Step = z.union([
  z.object({ cli: z.string().min(1), args: z.array(z.string()).default([]), stdin: TextSchema.default("") }).strict(),
  z.object({ script: RelPath, args: z.array(z.string()).default([]), stdin: TextSchema.default("") }).strict(),
]);

// Rows of one table, in the declared ORDER BY, serialized one JSON object per row.
const SqliteCapture = z.object({ db: RelPath, table: z.string().regex(/^[a-z_]+$/), orderBy: z.string().regex(/^[a-z_, ]+$/) }).strict();

const ModelResponse = z.object({
  bin: z.string().min(1),
  argsDigest: z.string().optional(),
  stdout: z.string(),
  exitCode: z.number().int().default(0),
}).strict();

const Expect = z.object({
  stdout: TextSchema,
  stderr: TextSchema,
  exitCode: z.number().int(),
  files: z.record(RelPath, z.string().nullable()),
  sqlite: z.record(z.string(), z.array(z.string())),
}).strict();

const Case = z.object({
  name: z.string().min(1),
  run: Run,
  args: z.array(z.string()).default([]),
  env: z.record(EnvKey, z.string()).default({}),
  stdin: TextSchema.default(""),
  setup: z.object({
    files: z.record(RelPath, TextSchema).default({}),
    steps: z.array(Step).default([]),
    sql: z.array(z.object({ db: RelPath, statements: z.array(z.string()) }).strict()).default([]),
  }).strict().default({}),
  capture: z.object({ files: z.array(RelPath).default([]), sqlite: z.array(SqliteCapture).default([]) }).strict().default({}),
  models: z.array(ModelResponse).default([]),
  expect: Expect.optional(),
}).strict();

const StartupBudget = z.object({ cli: z.string().min(1), args: z.array(z.string()), stdin: TextSchema.default(""), runs: z.number().int().min(1), medianMsMax: z.number().positive() }).strict();

const Suite = z.object({
  schema: z.literal("golden-suite/v1"),
  name: z.string().min(1),
  description: z.string().optional(),
  normalize: z.array(z.enum(NORMALIZERS)),
  // How record stores stdout/stderr: "plain" always, or "auto" — a reference
  // to the case's own stdin input when unchanged, base64 when the scrubber
  // finds a secret shape in it, plain otherwise.
  outputs: z.enum(["plain", "auto"]).default("plain"),
  inputs: z.record(z.string(), TextSchema).default({}),
  // One budget per CLI the suite covers (spec §4c: a startup-time budget per CLI).
  startupBudgets: z.array(StartupBudget).default([]),
  cases: z.array(Case),
}).strict();

export type Suite = z.infer<typeof Suite>;
export type Case = z.infer<typeof Case>;
export type Expectation = z.infer<typeof Expect>;
export type ModelResponse = z.infer<typeof ModelResponse>;
export type StartupBudget = z.infer<typeof StartupBudget>;
export type Inputs = Record<string, Text>;

function refsIn(t: Text): string[] {
  if (typeof t === "string") return [];
  if ("input" in t) return [t.input];
  if ("concat" in t) return t.concat.flatMap(refsIn);
  return [];
}

export function parseSuite(raw: unknown): { ok: true; suite: Suite } | { ok: false; error: string } {
  const r = Suite.safeParse(raw);
  if (!r.success) return { ok: false, error: r.error.issues.map((i) => `${i.path.join(".")}: ${i.message}`).join("; ") };
  for (const [id, t] of Object.entries(r.data.inputs)) {
    if (refsIn(t).length > 0) return { ok: false, error: `inputs.${id}: an input can't reference another input` };
  }
  const seen = new Set<string>();
  for (const c of r.data.cases) {
    if (seen.has(c.name)) return { ok: false, error: `duplicate case name: ${c.name}` };
    seen.add(c.name);
    const texts: Text[] = [c.stdin, ...Object.values(c.setup.files), ...c.setup.steps.map((s) => s.stdin), ...(c.expect === undefined ? [] : [c.expect.stdout, c.expect.stderr])];
    const missing = texts.flatMap(refsIn).find((id) => !(id in r.data.inputs));
    if (missing !== undefined) return { ok: false, error: `case ${c.name}: unknown input "${missing}"` };
  }
  return { ok: true, suite: r.data };
}

export function decodeText(t: Text, inputs: Inputs = {}): string {
  if (typeof t === "string") return t;
  if ("base64" in t) return Buffer.from(t.base64, "base64").toString("utf8");
  if ("input" in t) return decodeText(inputs[t.input] ?? "", {});
  if ("repeat" in t) return t.repeat.repeat(t.times);
  return t.concat.map((x) => decodeText(x, inputs)).join("");
}
