# Sindri (C#) — the context retrieval engine (S3)

Stage S3 of the Sindri app (factory addendum §17.29.2, §17.3; §17.17 Q2's recommendation: the query planner and evals
first, over the sources already reachable). It answers a question with ranked, scrubbed results from the sources the
approved profile turns on, and cites every result: source, locator, retrieval time. It lives inside `src/Sindri`
(`Core/Context`, `Core/Profile/*Context*`, `Features/QueryContext`, `Infrastructure/Context`) and plugs into S1's
command layer and API (`docs/csharp/sindri.md`).

Not in S3: the product graph, map review, the MCP host, Onyx, stored indexes or snippets (the design for those is open
PR #162, which plans a separate `Aw.Context` package; this stage keeps the code inside Sindri behind the same kind of
source port, so moving it later is a move, not a rewrite).

## Using it

```bash
sindri context query "<question>" [--sources code,notes,linear,connectai] [--limit N] [--json]
```

It is a client of the running control plane (start it with `sindri` first), like `status`.

| Exit | When |
|---|---|
| 0 | Results returned. A source that failed only degrades them (`DEGRADED` line, `failed` in the sources line) |
| 1 | No results, or a refused question or option (`SND-CONTEXT-003`) |
| 2 | Setup: the control plane isn't running (`SND-API-001`), no profile is approved (`SND-PROFILE-005`), or the profile turns on no source (`SND-CONTEXT-001`) |

Text output (no colour; state is always a word):

```
Context for "host lease fencing epoch" (terms: host, lease, fencing, epoch)
 1. [code] agentic-workflow/src/Sindri/Aw.Sindri.Infrastructure/Ledger/SqliteLease.cs:12
    // Acquire, renew and release the host lease, fenced by its epoch.
    retrieved 2026-10-11T07:10:02Z
 2. [notes] /Users/thor/notes/lease.md:3 (redacted)
    …
sources: code ok (10 hits); notes ok (2 hits); linear skipped (SND-CONTEXT-002 LINEAR_API_KEY is not set (fix: …))
```

`--json` prints `QueryContextResponse`: `question`, `terms`, `results[]` (`rank`, `title`, `snippet`, `score`,
`redacted`, `cite: { source, document, line, locator, retrievedAt }`), `sources[]` (`source`, `state` ok / skipped /
failed, `hits`, `detail`) and `degraded`.

API: `POST /sindri/context/query` with `{ "question": "…", "sources": ["notes"], "limit": 5 }` (sources and limit
optional), behind S1's loopback guard (a foreign `Host` is 400, a foreign browser `Origin` 403). Errors are
ProblemDetails with the `SND-*` code as `title`; a bad question is 400.

## Profile

An optional `context:` key joins schema version 1. A profile without it serializes to the same canonical JSON as before,
so its approval hash doesn't change. A source is **on** when its key is present.

```yaml
repos:
  - { name: agentic-workflow, path: /Users/thor/repos/agentic-workflow }
context:
  code: { repos: [agentic-workflow] }    # optional list; omitted: every profile repo. Needs at least one repo
  notes: { path: ~/notes }               # absolute, or starting with ~/
  linear: { teams: [ENG] }               # optional teams; the key comes from LINEAR_API_KEY, never the profile
  connectai: { server: connectai, tool: search }   # a port in this build: always skipped, with the reason
  limit: 10                              # 1 to 50, default 10
```

Errors are `SND-PROFILE-002` with the key path (`context.limit: must be 1 to 50`, `context.code.repos: 'x' is not a
profile repo`, …), like every other profile key. A changed `context:` needs `sindri profile approve` like any change.

## How a query runs

1. **Terms** (`QueryTerms`): words of letters, digits, `_` and `-` (so `ILedgerStore` and `SND-LEASE-001` stay whole),
   lower case, without common English words and one-letter words, at most 8; a question over 500 characters or without
   a searchable word is `SND-CONTEXT-003`.
2. **Sources** (`ISourceCatalog`): one opening per source the approved profile names, in source order. One that can't
   open is reported `skipped` with `SND-CONTEXT-002` and the fix; `--sources` marks the others `skipped (not asked
   for)`.
3. **Fan-out**: every opened source is searched in parallel with the terms and the limit, each with its own 4 s
   deadline. A source that errors or runs late is reported `failed` (`SND-CONTEXT-010`); the rest still answer.
4. **Fusion** (`RankFusion`): reciprocal rank fusion, k = 60, over each source's ranks (never its scores); the same
   locator from one source counts once; ties go to source order, then locator.
5. **Scrub** (`ITextScrubber`, adapter over `Aw.Redaction`'s built-in rules): every title, excerpt and document is
   scrubbed before it leaves; `redacted` marks a result where something was replaced or withheld.
6. **Cite**: every result carries `Citation(source, document, line, retrievedAt)`, `retrievedAt` being when its source
   answered.

No model is called and nothing is stored: the engine reads the sources live on each query.

## Sources

| Source | Adapter | Reads | Document | Notes |
|---|---|---|---|---|
| `code` | `GitGrepCodeSource` | `git grep` over each profile repo's tracked files: fixed strings, any term, ignoring case, binary skipped, ≤ 20 lines a file, ≤ 20 000 lines a repo | `repo/path` (+ line) | Terms go through `-e`, paths after `--`, no shell |
| `notes` | `NotesDirectorySource` | `*.md`, `*.markdown`, `*.txt` under the directory; hidden files and folders skipped; ≤ 5 000 files of ≤ 1 MB | the file's full path (+ line) | |
| `linear` | `LinearSource` | GraphQL `searchIssues` (read-only), optionally filtered to teams | the issue's permalink | Key from `LINEAR_API_KEY` (Authorization header, never in a message). Only the terms leave the machine |
| `connectai` | `McpContextSource` over `IMcpToolReader` | a read-only MCP search tool | the document's URI | Port and fake only: the real MCP client comes when ConnectAI's read surface is wired |

Code and notes rank documents with `LineMatches`: distinct terms held (in the name or the lines), then matching lines,
then name; the excerpt is the earliest line holding the most terms. Linear and ConnectAI keep their own order.

### Code index gap

`deps.yaml` has no `db` edge from the C# Sindri to the TS sindri's index database, and the C# app reads no TS state
(`docs/csharp/sindri.md`). So the `code` source is the git grep adapter above: lexical, no symbols, no call graph, no
embeddings. ripgrep isn't a dependency (git is, for every repo in the profile). The C# code index block (§17.29.4
step 4) replaces it behind the same `IContextSource` port; graphify joins with that block.

## Privacy

Retrieved text never leaves the machine in S3: the API is loopback only, no model is called, nothing is written to
disk. The one outbound call is Linear's API, when the profile names `linear` and a key is set, and it carries only the
search terms. The profile names which sources are on.

## Ports and their contracts

| Port (Core) | Contract (Aw.Sindri.Testing) | Fake | Real adapters |
|---|---|---|---|
| `IContextSource` | `ContextSourceContract`: best first, ≤ limit, case-insensitive, empty on no match, excerpt holds a term, cancelled → `SND-CONTEXT-010` | `FakeContextSource` | `GitGrepCodeSource`, `NotesDirectorySource`, `LinearSource` (over an in-memory GraphQL endpoint), `McpContextSource` (over the fake reader) |
| `IMcpToolReader` | `McpToolReaderContract` | `FakeMcpToolReader` | none yet |
| `ITextScrubber` | `TextScrubberContract` | `FakeTextScrubber` | `RedactionScrubber` |
| `ISourceCatalog` | `SourceCatalogContract` | `FakeSourceCatalog` | `SourceCatalog` |

Tests never call Linear except `LinearLiveTests` (heavy lane), which runs only with `LINEAR_API_KEY` set and is skipped
with that reason otherwise; nothing is recorded from it.

## Evals

`ContextEvalTests` (integration lane) runs a fixed question set through the real planner and the real code and notes
adapters over a fixture corpus (a git repo and a notes directory built by the test), scores each answer with
`EvalScore` (recall@5 and precision@5 against the documents a good answer cites; a result matches an expected document
when its document ends with it) and gates the means: recall ≥ 0.9, precision ≥ 0.3. The per-question numbers print in
the test output (`--output Detailed`). The bars are starting values; the backtest arms of §17.3 replace them when the
context graph lands.

## Errors

| Code | When |
|---|---|
| `SND-CONTEXT-001` | The approved profile turns on no context source |
| `SND-CONTEXT-002` | A named source can't open: no repo checkout, no notes directory, no `LINEAR_API_KEY`, ConnectAI not wired (reported per source, not fatal) |
| `SND-CONTEXT-003` | The question, a source name, an option or the limit isn't valid |
| `SND-CONTEXT-010` | A source didn't answer: unreachable, failed or past its 4 s deadline (reported per source; the answer is degraded) |

## Checks

```bash
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=unit"          # rules, planner, contracts over fakes, Linear over the in-memory endpoint, CLI text
dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=integration"   # git grep and notes adapters, catalog, the route on loopback, evals
LINEAR_API_KEY=… dotnet test --project tests/Aw.Sindri.Tests --filter-trait "Lane=heavy"   # the live Linear check
```
