# Golden contract suites

The contract each TypeScript package offers the rest of the toolkit, recorded from the TS build and replayed
against any build (spec `docs/superpowers/specs/2026-10-09-agent-readiness-design.md` §4a step 1, §4c). The C#
port of a package is "done" when its suites pass against the C# build (one cutover at parity, §4c).

| Suite | Covers |
|---|---|
| `judge/judge.suite.json` | Every `judge` subcommand, its errors and exit codes; questions through the model chain |
| `hooks/hooks-judge.suite.json` | The real hook scripts in `config/hooks/` that call `judge` (send-gate, scope-gate, done-gate, done-gate-annotate, subagent-start-map, judge-health), run with a `judge` shim on PATH |
| `redaction/redaction.suite.json` | A differential corpus through judge's `redactSecrets` and `sindri scrub`; findings in `redaction/DIFFERENCES.md` |

## Run

```bash
(cd judge && npm ci && npm run build) && (cd sindri && npm ci && npm run build)
(cd golden && npm ci && npm run build)
node golden/dist/cli.js replay goldens/*/*.suite.json            # against the TS build (targets.ts.json)
node golden/dist/cli.js replay goldens/*/*.suite.json --targets goldens/targets.cs.json      # the C# build (template: paths fixed when the projects exist)
node golden/dist/cli.js replay goldens/judge/judge.suite.json --target judge=/path/to/judge-cs   # one override (split on spaces: a path with a space needs a targets file)
node golden/dist/cli.js record goldens/judge/judge.suite.json    # re-record (TS bug fixes re-record in the same PR)
node golden/dist/cli.js redaction-diff goldens/redaction/redaction.suite.json > goldens/redaction/DIFFERENCES.md
```

## What a case pins

Command (`run.cli` = a target name from `targets.ts.json`, or `run.script` = a repo script) and args, an
allowlisted environment, stdin, stdout, stderr, exit code, the files it names under `capture.files`, and SQLite
rows under `capture.sqlite` (one table, a declared `ORDER BY`). Setup can write files, run steps and execute SQL.

- **Sandbox:** each case gets a fresh temp root: `HOME` and `AW_STATE_DIR` inside it, nothing inherited from the
  caller, `TZ=UTC`, `LC_ALL=C`, and `PATH` = fake model CLIs and a keychain stub, then target shims, then
  `/usr/bin:/bin`. A suite can't set those keys (`HOME`, `PATH`, `AW_STATE_DIR`, `TMPDIR`, `TZ`, `LC_ALL`,
  `NO_COLOR`, `GOLDEN_*`), and every path it names is relative with no `..` (checked by the schema and again at run
  time), so a case can't read or write outside its sandbox or point a hook at the live install.
- **Isolation is two layers.** The PATH stubs stop the TS build from reaching a model or the keychain. That alone
  doesn't hold for a build that links Security.framework or runs a CLI by absolute path, so on macOS every case and
  startup run also goes through `sandbox-exec` with a profile that denies outbound network (loopback allowed), the
  Security server's mach services and reads of `~/Library/Keychains`; a test proves a target that reaches for the
  network or the keychain is blocked. **On Linux only the PATH layer applies** (`golden` prints the isolation it
  used): replay a non-TS build there with networking off (`unshare -rn` or a network-less container).
  `--no-os-sandbox` turns the macOS layer off.
- **Normalization is declared per suite** (`sandbox-path`, `uuid`, `iso-timestamp`, `epoch-ms`,
  `duration-ms`) and applied before an exact comparison. Record refuses a declared rule that never fires, so each
  suite keeps the minimum (judge and hooks: `uuid`, `iso-timestamp`, `duration-ms`; redaction: none). Digests
  (judge's `input_digest`) are never normalized: they are part of the byte contract.
- **Fake model provider:** `claude`, `codex` and `cursor-agent` are stubs that answer from the case's `models`,
  keyed by a digest of their argv (sandbox paths and mkdtemp suffixes masked). Recording hands out the scripted
  responses in order and keys them; replay fails the case on any unrecorded input (the stub exits 97 and the
  harness reports `model calls`), and also fails a recorded response the build never asked for (the model-call
  set is part of the contract). Recording refuses a case with an unrecorded call or an unused response.
- **Startup budgets:** `startupBudgets` holds one per CLI the suite covers (judge `health`; `sindri scrub`;
  `judge-redact`). Each runs N times in a fresh sandbox and fails when the median exceeds `medianMsMax` or any run
  doesn't exit 0.
- **A broken target fails its case, never the run:** a setup step must exit 0, and any harness error is reported
  as that case's `harness` mismatch while the other cases still run.
- **Readable corpus:** suite-level `inputs` are shared by the judge and sindri cases. Text is a plain string, or a
  recipe: `{"base64"}` for secret shapes, `{"repeat", "times"}` for long inputs, `{"concat": [...]}`, or
  `{"input": id}`. With `"outputs": "auto"`, record stores an output that equals its input as `{"input": id}`, an
  output in which `sindri scrub` still finds a secret shape as base64, and everything else plain, so no
  secret-shaped literal sits in the repo and a re-record diff stays reviewable. `tests/goldens.test.ts` fails when
  `redaction/DIFFERENCES.md` is stale.

## The call recorder (replay week, B9) — not installed

`golden record-call --name <hook> [--env VAR]... -- <command> [args...]` records **inputs only** (argv, an
allowlisted env — `AW_SESSION_ID`, `AW_PROVIDER`, `AW_PROMPT_SORT_BUDGET_MS` plus `--env` — and stdin), scrubbed by
`sindri scrub` (one pattern source; `GOLDEN_SCRUBBER` overrides the binary), to
`${AW_STATE_DIR:-~/.agentic-workflow}/golden/recordings/<name>.jsonl` (dir 0700, file 0600, forced on every write;
a symlinked dir or file is refused). A field that is JSON (hook stdin always is) is scrubbed by its decoded string
leaves and keys, then re-serialized, so a token after a JSON-escaped newline is still redacted; unchanged JSON keeps
its original bytes. Then it runs the
command with the same stdin and passes its stdout, stderr and exit code through. If the scrubber fails, nothing is
recorded and the call still runs.

Nothing in `setup.sh`, the providers or the installed hooks uses it yet: the installed toolkit stays untouched
during the port. To turn it on for the replay week, wrap a hook's command in the provider settings, e.g.
`"command": "node <repo>/golden/dist/cli.js record-call --name send-gate -- ~/.claude/hooks/send-gate.sh"`.

## Findings from the first recording

- **Scrubber differential:** 19 of 36 corpus inputs redact differently in judge and sindri (see
  `redaction/DIFFERENCES.md`). judge lacks fine-grained GitHub PATs, AWS STS keys, Stripe, Google, Linear,
  bearer, credentialed-URL, assignment, AWS INI, SSN, MRN and private-key shapes; sindri deliberately lacks
  judge's bare 40+-hex rule (git SHAs, long hex runs, and a 20k run of `a`). Not fixed here: the tier-0
  `Redaction` package unifies them on one pattern file (§4c A2).
- **Both miss:** a token right after a JSON-escaped newline (`\nghp_…` in serialized JSON: no word boundary),
  Unicode look-alike prefixes, a zero-width joiner inside a token, and an unterminated private-key block.
