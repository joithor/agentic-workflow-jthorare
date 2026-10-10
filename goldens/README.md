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
node golden/dist/cli.js replay goldens/judge/judge.suite.json --target judge=/path/to/judge-cs   # another build
node golden/dist/cli.js record goldens/judge/judge.suite.json    # re-record (TS bug fixes re-record in the same PR)
node golden/dist/cli.js redaction-diff goldens/redaction/redaction.suite.json > goldens/redaction/DIFFERENCES.md
```

## What a case pins

Command (`run.cli` = a target name from `targets.ts.json`, or `run.script` = a repo script) and args, an
allowlisted environment, stdin, stdout, stderr, exit code, the files it names under `capture.files`, and SQLite
rows under `capture.sqlite` (one table, a declared `ORDER BY`). Setup can write files, run steps and execute SQL.

- **Sandbox:** each case gets a fresh temp root: `HOME` and `AW_STATE_DIR` inside it, nothing inherited from the
  caller, `TZ=UTC`, `LC_ALL=C`, and `PATH` = fake model CLIs and a keychain stub, then target shims, then
  `/usr/bin:/bin`.
- **Normalization is declared per suite** (`sandbox-path`, `uuid`, `iso-timestamp`, `epoch-ms`,
  `duration-ms`) and applied before an exact comparison. Digests (judge's `input_digest`) are never normalized:
  they are part of the byte contract.
- **Fake model provider:** `claude`, `codex` and `cursor-agent` are stubs that answer from the case's `models`,
  keyed by a digest of their argv (sandbox paths and mkdtemp suffixes masked). Recording hands out the scripted
  responses in order and keys them; replay fails the case on any unrecorded input (the stub exits 97 and the
  harness reports `model calls`). Recording refuses a case with an unrecorded call or an unused response.
- **Startup budget:** a suite's `startupBudget` runs one command N times in a fresh sandbox and fails when the
  median exceeds `medianMsMax`.
- **Secrets:** the redaction corpus is synthetic and its inputs and outputs are stored base64
  (`encodeOutputs`), so no secret-shaped literal sits in the repo.

## The call recorder (replay week, B9) — not installed

`golden record-call --name <hook> [--env VAR]... -- <command> [args...]` records **inputs only** (argv, an
allowlisted env — `AW_SESSION_ID`, `AW_PROVIDER`, `AW_PROMPT_SORT_BUDGET_MS` plus `--env` — and stdin), scrubbed by
`sindri scrub` (one pattern source; `GOLDEN_SCRUBBER` overrides the binary), to
`${AW_STATE_DIR:-~/.agentic-workflow}/golden/recordings/<name>.jsonl` (dir 0700, file 0600). Then it runs the
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
