# Sindri's own index: one database for records, chunks, vectors and the map (and Onyx goes away)

**Status:** design, revision 1, for the owner's review. No code until the owner has read it · **Date:** 2026-10-27 · **Owner decision:** 2026-10-27, drop Onyx; build a Sindri-owned index from ConnectAI's code, in the owner's words: "pulling out the code of connectAI but it becoming fully Sindri owned. not just another swap for another brain. a change to how we organize this stuff by making it a singular vectorized db that we can own and create the maps from" · **Builds on:** the context retrieval spec (`2026-10-10-sindri-context-retrieval-design.md`: D2, D3, §5, §6.2, §7, §8.2), the Onyx managed spec (`2026-10-16-sindri-onyx-managed-design.md`, retired by this one), the code index spec (`2026-10-19-sindri-code-index-design.md`), the whole-map spec (`2026-10-23-sindri-context-whole-map-design.md`), and the map-structure note (`2026-10-27-sindri-context-map-structure-design.md`: #1365, #1372, #1373) · **Lane:** context · **Next:** the owner reads this; then /specToProvenPR takes §12's phases to PRs.

ConnectAI paths below are relative to its repo root (a private TypeScript monorepo, read at a shallow clone on 2026-10-27; read only, nothing was run and no secret was read or copied). Sindri paths are relative to this repo.

## 0. Why

Onyx was meant to be a recall engine behind a port, with Sindri doing the ranking (retrieval spec D2, D3, `2026-10-10-sindri-context-retrieval-design.md:64-65`). Four things make that the wrong shape now:

| What | Today | Why it hurts |
|---|---|---|
| Sindri doesn't hold the text | A synced record keeps only its key, version, one-line title and aliases ("A record's text is never stored", `src/Context/Aw.Context.Infrastructure/Store/Migrations/0006_s5b_intent_links.sql:2`; `IntentRecord.cs:11`). Text is fetched live from Onyx or Linear (retrieval spec `:337`) | The map build cannot read it (map-structure note §11.1: "the map build does not see it"), and no embedding of it can join the graph |
| Sync is search-driven, not a feed | `IntentSyncRun` searches each source with terms taken from the map (`src/Context/Aw.Context.Features/SyncSources/IntentSyncRun.cs:134`) and fetches the hits (`:171`). `IChangeFeed` (`Aw.Context.Core/Sources/IChangeFeed.cs:6`) has no implementer | Sindri only ever sees records a term found. A record nobody searched for never arrives |
| Onyx is a second product to run | `context/onyx.lock:61-62`: 11.26 GB of disk and 5.9 GB of Docker memory, plus Docker, an OpenSearch, a Postgres, its own admin account and a personal access token for a dedicated user (`2026-10-16-sindri-onyx-managed-design.md` O7) | A one-user, one-box tool carries a multi-service stack it uses for one HTTP route (`POST /api/admin/search`, `OnyxSource.cs`) |
| Onyx can't be given Sindri's ids | Its hits are Onyx documents; Sindri maps them to keys after the fact (`OnyxRecordKeys.cs:30-55`) | Vectors and graph nodes can't share ids, so "find the records near this feature" is a join Sindri can't do |

**This is not an adapter swap.** ConnectAI is a company-brain server (Postgres, pgvector, a loop, an MCP server). What Sindri takes from it is the part that pulls data out of tools and turns it into searchable chunks: connector mechanics (paging, cursors, rate-limit handling, per-tool field maps), the text cleaner, the chunker, and the idea of hybrid dense plus lexical recall. What Sindri does **not** take is its data model (it deliberately keeps no structured record mirror, §14.4), its multi-tenant plumbing, or its ranking. The index is a part of Sindri's own context database, so the map is built from it and its ids are the graph's ids.

## 1. Decisions

| # | Decision | Reason |
|---|---|---|
| I1 | **One store: new tables in the existing `$AW_SINDRI_HOME/context/context.db`** (SQLite, WAL, through `Aw.Kernel.Data`): structured source records, chunks (with FTS5), vectors. Not a second file, not a server | The graph is already in this file (`0002_s3a_graph.sql`). A join between a record, its chunks, its vector and its node is one SQL statement and one transaction (§4.1) |
| I2 | **Vectors are float32 blobs in SQLite, searched by an in-process scan** over a RAM copy, not sqlite-vec and not Postgres with pgvector. The scan is behind a port, so an index structure can replace it when a measured number says so (§4.4) | No new native library or server. It is how the code index already stores and reads vectors (`src/CodeIndex/Aw.CodeIndex.Infrastructure/Store/VectorCodec.cs:42`, `.../SqliteIndexReader.Clones.cs:104`) |
| I3 | **The text we store is scrubbed text**, under the profile's ruleset, with the ruleset recorded per record (the repo-docs pattern, `0003_s2w_scrub_ruleset.sql`) | Retrieval spec §7 "Stored text". This **reverses** "record text is fetched live, never stored" (`:337`) and needs the owner's yes (Q7) |
| I4 | **Connectors are a new tier-1 package, `Connectors`**, a port of ConnectAI's connector mechanics in idiomatic C#. First: GitHub (issues and pull requests) and Slack. Linear stays where it is (the existing native adapter becomes a feed) | §5.1 to §5.3. GitHub issues and Slack reach Sindri today only through Onyx (`docs/sindri/onyx.md:177,183-186`) |
| I5 | **Embeddings come from a new tier-0 package, `Embeddings`**: the loopback Ollama client, the vector codec and the scan. The model stays the code index's pinned `nomic-embed-text` (`context/code-index.lock`) | One model for code and records, nothing new to install (the model is already pulled by `scripts/install-deps.sh:155-195`) |
| I6 | **Ranking is Sindri's and uses the graph.** Search is two legs over the store (dense, lexical) plus the graph legs that exist today, fused by reciprocal rank fusion, then the feature boost. One new graph signal (reach from the top hits) and two record signals (state, freshness) are added to the five that exist (§6) | The five features are `RankFeatures.cs:12-55`; fusion is `RankFusion.cs:13` and `FeatureRanker.cs:41` |
| I7 | **The map builds from the store.** Doc records, cited paths, record age and the tracker half of #1373 read the store, not `IDocIndex` and not the read-time overlay alone (§7) | Closes map-structure note §11.1 at the root |
| I8 | **Credentials stay Sindri's own pattern**: a secret pointer in the profile (`env:`, `file:`, `keychain:`, `op:`), a read-only token the owner makes, scopes limited to the profile's repos, teams and channels. ConnectAI's vault, GitHub App private key and service-account approach are not copied (§5.4) | `LinearSettings.cs:17`, `ISecretPointers.cs:10`. Retrieval spec §3: "Sindri never connects a business source itself" |
| I9 | **Onyx is removed** in this order: the new index serves and backtests at parity, then the `sindri onyx` verbs, the Onyx adapter, the pins, the setup step and the docs go in a sequence of small PRs (§8) | The backtest arms still read Onyx (`Aw.Context.Infrastructure/Eval/OnyxBriefSource.cs`) |
| I11 | **No model call on ingest**: no summary, fact, entity or procedure extraction. ConnectAI makes up to five per item; Sindri makes none (§5.7) | Box cost, egress, and the graph's own linker already does the useful part |
| I10 | **No Docker for the index.** Docker stays on the box for Serena only (`setup.sh:268-290`) | The owner's goal: nothing to start, nothing to log in to |

## 2. Goals and non-goals

Goals: the owner connects GitHub, Slack and Linear by pasting read-only tokens into the profile and running `sindri index sync`; every record in scope arrives, with its state, links and attachments; a query returns it by meaning and by exact words, ranked with the graph; the map build reads the same rows the search does; nothing outside Sindri's own process runs.

Non-goals:
- No multi-tenant anything: one owner, one box, one `context.db` (README X61 keeps "shared later, not foreclosed": a `Shared` source placement stays in the contract, `SourcePlacement.cs`).
- No model call on ingest. ConnectAI also writes a derived summary line per record and embeds it beside the raw windows (`apps/api/src/application/services/ingest/chunk-synthesize.ts:1-23`: a derived path and a raw path); Sindri embeds the scrubbed source text only. Model egress stays behind the profile switches of retrieval spec §3.
- No write-back to any tool (ConnectAI's `act()`, `packages/connectors-core/src/types.ts:310-336`, is not ported).
- No history of a record's versions. The store keeps the latest version and the times the source gives (created, updated, closed).
- No change to graphify, to the map-structure steps 3 to 5 (S3, S4, S5), or to the code index's own files (§7.4).
- No rewrite of ConnectAI. It keeps running as it is; nothing here changes it.

## 3. Terms

- **Record:** one thing a source holds: a Linear issue, a GitHub pull request, a Slack thread, a repo doc. It has a **record key** (the existing `RecordKey`, `Aw.Context.Core/Sources/RecordKey.cs:12`), such as `linear:issue/<uuid>` or `repo:<repo>/<path>`.
- **Chunk:** a window of a record's scrubbed text, about 400 tokens, with a small overlap. Its key is `<record key>#<seq>`.
- **Vector:** the embedding of a chunk (or of a graph node), stored under the same key as what it embeds.
- **Source:** where records come from (`linear`, `github`, `slack`, `repo-docs`). A **connector** is the code that reads one.
- **Feed:** a connector's way to hand over what changed since a cursor, a page at a time.
- **Leg:** one ranked list in a query (dense, lexical, graph, code).
- **Seed:** one of the top hits of the dense and lexical legs, used as a starting point for the graph leg.

## 4. The store

### 4.1 Where, and why this engine

Measured on the dev box for this spec: 16 cores, 31 GB RAM, 73 GB free disk (`nproc`, `free -g`, `df -h`, 2026-10-27). Cited for the rest.

| Option | Needs | Verdict |
|---|---|---|
| **A. SQLite + FTS5 + float32 blobs, scanned in process** | Nothing new. `Microsoft.Data.Sqlite` with its bundled `e_sqlite3` is already the engine (`Directory.Packages.props:36-38`; AGENTS.md Tech Stack "Database"). FTS5 is already proven in the bundled build (`0001_s1a_repo_docs.sql`) | **Chosen** |
| B. SQLite + the sqlite-vec loadable extension | A native library per OS and CPU (four builds) shipped beside `e_sqlite3`, pinned and verified like the other binaries; extension loading enabled on the connection; AOT packaging to prove. As I read it, sqlite-vec is also a full scan with SIMD, so it is not a different algorithm, only faster code (verify in phase I0) | Held back: adopt only if A's measured p95 misses its budget (§4.4) |
| C. Postgres + pgvector (ConnectAI's engine: `packages/database/prisma/manual-indexes.sql:25,34-35`, HNSW on 1,024-dimension cosine; image `pgvector/pgvector:pg16`, `README.md:179`) | A server process and a second engine beside the SQLite stores (`planning/ERD.md` is all SQLite); Docker or a native install; a graph in another file from its vectors. Cost at our size: the server idles at a few hundred MB (the image alone is several hundred MB; the Onyx stack that includes a Postgres is 5.9 GB of Docker memory and 11.26 GB of disk, `context/onyx.lock:61-62`), HNSW adds an in-memory-resident graph of about 1.5 to 2 times the vector bytes (≈ 300 MB at 50,000 chunks of 768 floats; my estimate from pgvector's published sizing, not measured here), and an HNSW build over 250,000 vectors is minutes of CPU | Rejected: it brings back what we are removing, and the join with the graph is gone |
| D. LanceDB or another embedded vector store | A new native dependency and file format; the factory addendum lists it as an option (`2026-10-09-sindri-factory-addendum.md:2135`) | Rejected for now: one more thing to pin, and no number says A is too slow |

**Where:** the same `context.db`, not a new file. One transaction can write a record, its chunks and its vector rows; one query can join `source_record` to `node` through the key (§4.3). The cost is a bigger file with big blob rows beside small graph rows; WAL checkpoints and `VACUUM` run in the existing purge service. The code index's per-repo immutable files stay as they are (they are derived data published by pointer, code index spec §4.1); the store is the one place for everything else.

### 4.2 Schema (migration `0016`, `planning/ERD.md` gets the same tables)

```sql
-- One row per record. The structured facts a tracker gives: not only text.
CREATE TABLE source_record (
    key         TEXT PRIMARY KEY,                 -- RecordKey: linear:issue/<uuid> | github:pr/<owner>/<repo>/<n> | github:issue/... | slack:<channel>/<ts> | repo:<repo>/<path>
    source      TEXT NOT NULL,                    -- linear | github | slack | repo-docs
    kind        TEXT NOT NULL CHECK (kind IN ('doc','issue','pr','comment','project','message')),
    repo        TEXT NULL,                        -- the profile repo it belongs to, when it has one
    title       TEXT NOT NULL,                    -- scrubbed, one line
    url         TEXT NULL,                        -- only an allowed scheme and host (Permalinks)
    author      TEXT NULL,                        -- scrubbed
    state       TEXT NULL CHECK (state IS NULL OR state IN ('open','closed','merged','draft','canceled','done')),
    state_raw   TEXT NULL,                        -- the source's own word ("In Review"), scrubbed
    created_at  TEXT NULL, updated_at TEXT NULL, closed_at TEXT NULL,
    version     TEXT NOT NULL,                    -- the source's version: updatedAt, or a git blob id for a doc
    text_hash   TEXT NOT NULL,                    -- sha-256 of the scrubbed full text; a vector is recomputed only when it changes
    ruleset     TEXT NOT NULL,                    -- the scrub ruleset it was cleaned with
    blocked     INTEGER NOT NULL CHECK (blocked IN (0,1)),   -- the scrubber withheld the text: the row stays, it has no chunks
    removed_at  TEXT NULL,                        -- the source deleted it: a tombstone, so links to it stay readable
    synced_at   TEXT NOT NULL
) STRICT;
CREATE INDEX source_record_by_source ON source_record (source, updated_at);

-- What the source itself says connects two records: a pull request that closes an issue, an issue's parent, a Linear attachment.
CREATE TABLE record_link (
    from_key   TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    to_ref     TEXT NOT NULL,                     -- the other record's key when known; `path:<repo>/<path>` for a file a PR touched (resolved at map build, §7.5); else the url as the source gave it (scrubbed)
    to_key     TEXT NULL,                         -- filled when to_ref resolves to a stored record
    kind       TEXT NOT NULL CHECK (kind IN ('parent','blocks','duplicates','relates','closes','attachment','review-of','replies-to','in-project','touches')),
    title      TEXT NULL,
    PRIMARY KEY (from_key, to_ref, kind)
) STRICT;

CREATE TABLE chunk (
    id         INTEGER PRIMARY KEY,
    key        TEXT NOT NULL UNIQUE,              -- <record key>#<seq>
    record     TEXT NOT NULL REFERENCES source_record (key) ON DELETE CASCADE,
    seq        INTEGER NOT NULL,
    text       TEXT NOT NULL,                     -- scrubbed
    tokens     INTEGER NOT NULL,
    line_start INTEGER NULL, line_end INTEGER NULL   -- docs keep their line span (as doc_chunk does)
) STRICT;
CREATE INDEX chunk_by_record ON chunk (record, seq);
CREATE VIRTUAL TABLE chunk_fts USING fts5 (text, content = 'chunk', content_rowid = 'id', tokenize = 'unicode61');   -- plus the three triggers doc_chunk has

-- One vector per thing embedded, under the same key as the thing.
CREATE TABLE embedding (
    space     TEXT NOT NULL CHECK (space IN ('chunk','node')),
    key       TEXT NOT NULL,                      -- a chunk key, or a graph NodeKey
    model     TEXT NOT NULL,                      -- name and digest from the pin
    text_hash TEXT NOT NULL,                      -- of what was embedded; a row with another hash is stale
    dim       INTEGER NOT NULL,
    vec       BLOB NOT NULL,                      -- float32 little-endian, L2-normalised (so cosine is a dot product)
    PRIMARY KEY (space, key, model)
) STRICT, WITHOUT ROWID;

-- source_cursor (0001) gains: phase TEXT NULL, backfill_cursor TEXT NULL, lease_until TEXT NULL.
```

How the old tables map: `doc` becomes `source_record` rows of kind `doc` (`blob` is `version`, `title`, `blocked`, `ruleset` carry over); `doc_chunk` becomes `chunk` (line spans carry over). Migration `0016` creates the new tables and copies; `0017`, one release later, drops `doc` and `doc_chunk`. Copied docs have no vectors yet: the embed job (§5.6) fills them. `intent_record` and its links, decisions and conflicts (`0006`) stay exactly as they are; its `key` is the same string as `source_record.key`. It keeps the linker's bookkeeping (`extraction_state`, `linked_map`). Its `title`, `version` and `aliases` repeat what `source_record` holds; folding them is a later clean-up (Q13).

### 4.3 Ids shared with the graph

One rule: **the graph node of a record is `record:` plus its record key** (`IntentOverlay.cs:123`, `RecordNode`; repo docs already follow it, `RepoDocs.Key`, `RepoDocs.cs:17`). A chunk's key is `<record key>#<seq>`; its vector row has that key. A graph node's vector (`space = 'node'`, §7.3) has the node's own key (`feature:...`, `concept:...`). A code node is `code:<path>` and is not stored in `source_record` (code text is read from git at the sha, retrieval spec `:337`); its vectors stay in the code index (§7.4).

So from a chunk to its node: strip `#<seq>`, prepend `record:`. From a node to its chunks: strip `record:`, `WHERE record = ?`. No lookup table, no second id space.

### 4.4 Search cost and the budget

Vectors are L2-normalised on write, so a query is one dot product per stored vector. The existing code index loops in `double` with three accumulators per pair (`VectorCodec.cs:42-50`); the store uses a `float` loop over `System.Numerics.Vector<float>` (BCL, NativeAOT-safe) on a RAM copy of the matrix, loaded once by `serve` and refreshed when the store's commit stamp moves (`Store/StampedCache.cs` is the pattern). Memory is the limit, not arithmetic.

Sizes are arithmetic (n chunks times 768 floats times 4 bytes), not measurements; the corpus size is an assumption that phase I0 replaces with the owner's real counts (it prints them from the sources' list endpoints):

| Chunks (assumed corpus) | Vector matrix, float32 | One scan at 10 to 20 GB/s | Store file on disk (vector 3 KB + text about 2 KB + FTS about 1.5 KB per chunk) |
|---|---|---|---|
| 50,000 (three Linear projects, a year of PRs) | 154 MB | 8 to 15 ms | about 0.3 GB |
| 250,000 (plus Slack threads) | 768 MB | 40 to 77 ms | about 1.6 GB |
| 1,000,000 | 3.1 GB | 150 to 300 ms | about 6.5 GB |

**Budget:** p95 of the whole dense leg under 150 ms at the owner's real size on this box. Up to about 100,000 chunks the matrix is kept as float32 in RAM (307 MB). Above that, the cache holds int8 vectors (a scale per vector, a quarter of the memory) and the top 200 are re-scored exactly from the float32 rows. Past 1,000,000, or when the measured p95 misses the budget, option B or an approximate index goes behind the same `IVectorScan` port; no other code changes. Which of those and when is a phase I0 number, not a guess made here (Q2).

The scope rule (retrieval spec §3, "Repo scope") applies **inside** the scan: the caller's allowed record set is a mask over the matrix before the top 50 are taken, never a filter after it, or a session whose repo owns few records would get an empty leg.

### 4.5 What is stored, and what stays out

Stored: scrubbed text, titles, authors, urls (allowed hosts only), states, times, the links a source states. Not stored: unscrubbed text (a record the scrubber blocks keeps its row, `blocked = 1`, and no chunks), attachments' contents (their title and url only), comments' author handles beyond the scrubbed display name, direct messages (never read: the Slack allowlist names channels, §5.4), tokens (never, §5.4).

The file is `0600` in a `0700` directory, as `context.db` is now (retrieval spec §3 "State"); full-disk encryption is assumed, as there. When the profile's scrub patterns change, the ruleset id changes, every record under the old one is skipped by search (as docs are, `0003`) and marked for a re-pull; the next sync fetches it again, scrubs it, and re-embeds only when `text_hash` changed.

## 5. Sources, connectors and sync

### 5.1 What ConnectAI has, and what we do with each

ConnectAI's 18 connector directories are in `packages/connections/src/connectors/` (its README lists 17 (`README.md`, "Connectors"), plus `paperclip`, an agent-history source).

Confidence: **code** = the transport and connector files were read for this spec or the ideas lane's inventory; **spec** = only the connector's `SPEC.md` and header comments were read (inventory `connectai-inventory.md` §2.1 note: "treat their field lists as spec-level, not verified in code"). Lines are transport, server, connector (`wc -l`, 2026-10-27).

| # | Connector | What it reads | Lines | Confidence | Decision |
|---|---|---|---|---|---|
| 1 | **GitHub** | Issues and pull requests: state, label and comment **counts**, review counts, change size (`github-connector.ts:41-55`); `/issues` with `since`, `/pulls` filtered locally (`github/transport.ts:475-522`). No comment text, review text, diffs or links | 618, 231, 201 | code | **Port first.** No Sindri adapter exists and it is where the owner's issues and PRs live |
| 2 | **Slack** | Channel history, each thread root expanded through `conversations.replies`, a permalink per thread through `chat.getPermalink`; per-channel `ts` watermark (`slack/transport.ts:7-13,110-153`; `types.ts:219-228`) | 304, 147, 210 | code for the history and replies loop and the rate-limit note; spec for the rest | **Port second.** Reaches Sindri only through Onyx today (`docs/sindri/onyx.md:177`). Watch the rate limit in §5.3 |
| 3 | Linear | Issues and comments, `orderBy: updatedAt` (`linear/transport.ts:28-36`), state `{id,type}`, no relations or attachments | 420, 253, 229 | code | **Not ported.** Sindri's native adapter reads more (projects, documents, updates, attachments, relations: `Aw.Context.Infrastructure/Linear/Operations/*.graphql`). It becomes a feed (§5.2) |
| 4 | Jira | Issues via JQL, ADF flattened to text, every comment (an extra call per issue), parent and epic links (`jira/transport.ts:264-267`) | 524, 197, 215 | code | Later, if the owner uses it (Q4) |
| 5 | Notion | Pages and database rows, block text | 266, 140, 211 | spec | Later, if the owner uses it (Q4) |
| 6 | Granola | Meeting notes: summary and transcript | `granola/*` | spec | Later; transcripts are long, so the chunker matters more than the connector |
| 7 | Zoom | Cloud-recording transcripts (VTT) | `zoom/*` | spec | Later, same |
| 8 | Gmail | Mail threads | `gmail/*` | spec | **Not ported.** Business mail is not product context |
| 9 | Google Calendar | Events | `calendar/*` | spec | **Not ported** |
| 10 | Microsoft 365 | Mail, calendar, OneDrive, Teams | `microsoft-365/*` | spec | **Not ported.** Its tenant-wide "sovereign" mode (`oauth/SOVEREIGN.md:1-20`) is what we do not copy (§5.4) |
| 11 | Calendly | Events and invitees | `calendly/*` | spec | **Not ported** |
| 12 | Discord | Channel messages | `discord/*` | spec | **Not ported.** The only connector with an in-transport retry on `retry_after`; Slack's client takes that idea (§5.3) |
| 13 | HubSpot | CRM deals | `hubspot/*` | spec | **Not ported** (CRM) |
| 14 | Attio | CRM records | `attio/*` | spec | **Not ported** (CRM) |
| 15 | Intercom | Support conversations | `intercom/*` | spec | **Not ported** (helpdesk) |
| 16 | Stripe | Invoices, subscriptions | `stripe/*` | spec | **Not ported** (payments) |
| 17 | QuickBooks Online | Accounting records | `quickbooks-online/*` | spec | **Not ported** (accounting) |
| 18 | Paperclip | An agent-orchestration product's issues, runs, decisions | `paperclip/*` | spec | **Not ported.** Sindri's own agent sessions are `sindri agents` |

Why this order: GitHub first because it is the one tracker with no Sindri reader, it holds the PRs that connect issues to code (§7.5), and its transport (paging, rate limit, the cursor boundary rule) is fully readable in code. Slack second because it is the other source that reached Sindri only through Onyx and its keys already match (§8.3). Linear is already read natively, so porting ConnectAI's weaker reader would be a step back. Everything else waits for the owner to name a tool they use.

What else we take, and how:

| ConnectAI piece | Where | In Sindri |
|---|---|---|
| The connector contract: `read(window) -> { records, nextCursor, cursorReset }`, read kinds, `healthCheck` | `packages/connectors-core/src/types.ts:162-204,292-305,339-348` | `IConnector.Pull(cursor, limit)` returning a page of full records and the next cursor, in `Aw.Connectors.Core` (§5.2). No `act` |
| Sync phases: cold start on a recent window, resumable backfill, incremental by cursor, daily reconcile | `apps/loop-svc/src/sync/phases.ts:40-50` (30 days, pages of 10, 90 days) | The same four phases (§5.5); sized from the owner's profile, not from ConnectAI's cost limits |
| Single flight per connection with a fencing lease | `apps/loop-svc/src/sync/claim.ts`, `schema.prisma:463-480` | Not ported: Sindri has `SyncLocks` and one `serve` host (`SyncLocks.cs`) |
| Token-aware windowing, 400 tokens with 40 overlap | `connectors/raw-window.ts:34-36,46-88` | `Chunker` in `Aw.Context.Core` (§5.6) |
| Text cleaning: quoted-printable, HTML to text, control characters, whitespace | `connectors/raw-clean.ts:43-266` | `TextCleaner` in `Aw.Connectors.Core`, the HTML and whitespace parts only (no mail decoding) |
| GitHub rate-limit handling, the `since` cursor with a seen-id set at the boundary | `github/transport.ts:111-135,137-210` | Read as the spec for the C# client's error mapping and cursor, not translated |
| Reciprocal rank fusion, k = 60 | `apps/api/.../retrieval/rrf.ts:47-89` | Already in Sindri, same constant (`RankFusion.cs:13`) |
| Spreading activation over the entity graph: 2 hops, keep 7, fan-out 50 | `retrieval/spreading-activation.ts:43-62` | Ported as the reach signal, over Sindri's typed graph instead of entity edges (§6.2) |
| ACT-R recency re-rank: 0.7 similarity + 0.3 activation, decay 0.5, with a write on every read | `retrieval/actr-rerank.ts:45-52,113-122` | **Not ported.** A read that changes the next ranking makes results unrepeatable and breaks the backtest (D13); Sindri's recency is the age of the link and the record, computed, never written (§6.2) |
| Entity edges with weight and reinforcement | `schema.prisma:1081-1112`, `edge-reinforce.ts` | Not ported: Sindri's edges are typed and carry provenance; weights come from type and provenance (`RankFeatures.cs:12-33`) |
| Vault, inference gateway with budgets, MCP server, billing, RLS | `packages/vault`, `packages/inference`, `apps/api/src/routes/mcp` | Not ported: Sindri has secret pointers, a loopback Ollama and its own MCP |

ConnectAI is under the Business Source License 1.1 and is the owner's own code (`README.md`, "Licensing"). Nothing here copies a file: each item is a reimplementation in idiomatic C# with the TypeScript as a guide, the rule in `.agents/rules/csharp.md` "Porting a TS package" (Q17).

### 5.2 The contract, and what changes in Sindri's own

```
Aw.Connectors.Core
  IConnector       Describe() ; Pull(SourceCursor? after, int limit, ct) -> ConnectorPage ; Versions(keys, ct) ; Probe(ct)
  ConnectorPage    Items: IReadOnlyList<ConnectorItem> ; Removed: IReadOnlyList<string> ; Next: SourceCursor ; CursorReset: bool
  ConnectorItem    Key, Kind, Title, Text, Author, Url, State, StateRaw, CreatedAt, UpdatedAt, ClosedAt, Version, Links[], Repo?
```

It is neutral on purpose: `Connectors` is tier 1 and a tier-1 package cannot reference another tier-1 package (§9.1), so it cannot use `Aw.Context.Core`'s `SourceRecord`. **Sindri's Host is the glue**, as it already is for the code index (`ContextFeaturesServiceCollectionExtensions.cs:95` is the `ISymbolVectors` seam): a small adapter maps a `ConnectorItem` onto Context's `RecordBatch` (§5.5). This also means the existing `SourceRecord` (`SourceRecord.cs:11`: title, text, author, two dates, no state, no links) is extended in Context with `State`, `StateRaw`, `ClosedAt` and `Links`, for the sources that live inside Context (Linear, repo docs).

Linear stays an `IContextSource` inside Context. It gains a `Pull`: a new list operation (`Issue.graphql` fetches one issue by id; `IssueHits.graphql` searches by terms), filtered on `updatedAt > cursor` and ordered by `updatedAt`, plus the issue state (`Issue.graphql:4` already asks for `state { name type }`; `LinearIssue` has no field for it, `LinearIssue.cs:3-17`). Linear's `orderBy: updatedAt` sorts newest first (ConnectAI reads it so, `linear/transport.ts:30`; the inventory flags that a capped read could then skip older items, open question 3), so `Pull` pages newest first until it passes the cursor and commits the new cursor only when it has reached the old one; a run cut short repeats work and loses nothing. `IChangeFeed`, which has no implementer today, is replaced by a `RecordFeed` port in `Aw.Context.Core/Sync/` with the same page shape as `IConnector.Pull`; Linear and repo docs implement it inside Context, and the Host adapter implements it over each `IConnector`. So there is **one sync driver** in `Aw.Context.Features` (the existing `SyncSources` slice, rewritten to pull feeds), for every source.

### 5.3 The first two connectors

**GitHub** (`Aw.Connectors.Infrastructure/GitHub/`): one `HttpClient` to `api.github.com` (or an `https` Enterprise host the profile names), no redirect to another host, per repo in the profile's `context.sources.github.repos`:
- Issues and pull requests through `GET /repos/{o}/{r}/issues?state=all&sort=updated&direction=asc&since=<cursor>&per_page=100` (this endpoint returns both; a record with a `pull_request` field is a PR; ConnectAI's transport reads `/issues` this way and reads `/pulls` separately, newest first, filtering locally, `github/transport.ts:473-522`; Sindri needs one list, so it uses `/issues` and fetches the PR detail only for PRs), then for a PR `GET /pulls/{n}` once for `merged`, `draft`, `merged_at` and the change counts (ConnectAI asks the `/pulls` *list* for those counts, `transport.ts:340-343`, which I believe omits them: inventory open question 2, settled by taking them from the single-PR route) and `GET /pulls/{n}/files` (100 per page, at most 3 pages) for the paths it changed (§7.5). Comments and review bodies, which ConnectAI never fetches (inventory §2.1: counts only), are fetched for records updated since the cursor, newest first, capped per record by a profile key (default 100 comments).
- State: `open`, `closed`, `merged`, `draft`. Links: `closes`/`fixes #n` in the PR body and linked issues from the timeline (`closes`), the PR's issue (`relates`), a cross-reference in a comment (`relates`).
- Cursor: the newest `updated_at` seen plus the ids seen at exactly that instant (ConnectAI's boundary rule, `github/transport.ts:137-210`), so a page boundary inside one timestamp loses nothing and repeats nothing.
- Rate limit: on `x-ratelimit-remaining: 0` or a secondary-limit reply the run stops cleanly with the cursor it has, records `Unavailable` with the reset time, and the next run continues. At 5,000 requests an hour (authenticated REST) and 100 records per page, 20,000 records are about 200 list requests.
- Key: `github:issue/<owner>/<repo>/<n>` and `github:pr/<owner>/<repo>/<n>`. (ConnectAI keys by GitHub's `node_id`, `github-connector.ts`, for cross-API stability; Sindri's keys are read by people and by the linker's `url` and `issue-key` rules, so they carry the repo and number, and the `node_id` goes in `version`'s companion, not the key. A renamed repo re-keys through `IKeyMigration`, `Aw.Context.Core/Graph/IKeyMigration.cs`.)

**Slack** (`Aw.Connectors.Infrastructure/Slack/`): `conversations.history` and `conversations.replies` for the channels in `context.sources.slack.channels`, never a direct message or a channel not listed. A thread (a `thread_ts` chain) is one record; a busy channel's unthreaded messages are bucketed by day. Key `slack:<channel>/<ts>`, **the same key Onyx's Slack connector produces** (`OnyxRecordKeys.cs:48-52`), so every stored link and decision on a Slack thread stays valid (§8.3). Cursor: the newest `ts` per channel, as ConnectAI's per-channel watermark map (`types.ts:219-228`). The thread link comes from `chat.getPermalink` on the root (`slack/transport.ts:7-9`).

**A rate limit that can decide the connector's value.** ConnectAI's transport assumes Slack's regime for apps outside the Marketplace: 1 request a minute and at most 15 objects per `conversations.history` call (`slack/transport.ts:13-15,25-26`). At that rate a backfill reads 900 messages an hour: a channel with 20,000 messages takes about a day, each thread's `conversations.replies` costs another request, and 90 days of ten busy channels is a week. As I understand Slack's policy the limit applies to commercially distributed apps and not to an internal app the owner creates in their own workspace, which keeps the normal tier (about 50 requests a minute, 999 messages per call). I have not verified that, so phase I0 makes one call with the owner's token and reads the `Retry-After` header and the page cap; Q16 asks which kind of app it is. The client honours `Retry-After` and resumes from its cursor (as the Discord connector does, inventory §2.1).

### 5.4 Credentials

| | ConnectAI | Sindri |
|---|---|---|
| Where a token lives | A vault (Infisical, or a local encrypted file) under `cred_<id>`, one per workspace (`packages/vault/src/vault.ts:20-45`); a platform-wide GitHub App private key plus a per-workspace installation id (`github/app-auth.ts:1-5`) | A **secret pointer** in the profile: `env:NAME`, `file:/path` (mode 0600), `keychain:service/account`, `op:vault/item/field` (`LinearSettings.cs:17`; resolved by `ISecretPointers.Resolve`, `ISecretPointers.cs:10`). A pointer that isn't one is refused and never echoed (`LinearSettings.cs:53-58`) |
| What the token can do | GitHub: an App with permissions across installations; Google: a service account with domain-wide delegation that can read any mailbox of the domain; Microsoft 365: tenant-wide app permissions (`SOVEREIGN.md:11-18`) | **A read-only token the owner makes, limited to what the profile lists.** GitHub: a fine-grained personal access token with Issues, Pull requests and Metadata read, on the listed repos only. Slack: a bot token with `channels:history`, `channels:read` (and `groups:*` only for listed private channels), in an app the owner installs. Linear: the read-only key the profile already points at |
| Who connects | A user clicks Connect and consents | The owner, with company sign-off where the tool is the company's (retrieval spec §3 "Company OAuth"). Sindri never creates an OAuth app, never asks for a grant, never stores a password |
| Scope | The workspace | The profile: `context.sources.github.repos`, `.slack.channels`, `.linear.teams` (`LinearSettings.cs:33`); a record outside them is dropped at the connector, before it reaches the store |
| Printing | | `ToString` withholds the pointer (`LinearSettings.cs:66`); no error repeats a token; a test plants a token in the environment and asserts it reaches no log, no span and no file (retrieval spec §7 "Logs and spans") |

A `sindri index connect <source>` verb, a human-only one (`SND-AUTH-002` for a session, as `onyx up` is), asks for the token once at the terminal and writes it with the existing `KeychainSecretWriter` (`Aw.Sindri.Infrastructure/Secrets/KeychainSecretWriter.cs`), then writes the pointer into the profile; it is the one part of the Onyx machinery that stays (§8.1).

### 5.5 Sync

Phases per source, from ConnectAI's four (`phases.ts:5-17`), run by the one driver:

| Phase | What it reads | Default |
|---|---|---|
| Recent | Records updated in the last N days, newest first, so the first answers come in minutes | 30 days (`phases.ts:40`) |
| Backfill | Older records, one page per tick, resumable from `backfill_cursor` | pages of 100 (ConnectAI uses 10 because each page costs a model call; Sindri's page costs an HTTP call and an embed) |
| Incremental | Changes since `cursor` | every 5 minutes while `serve` runs |
| Reconcile | A bounded full window, to catch deletes and edits the cursor missed; marks records the source no longer returns as `removed_at` | daily, 90 days (`phases.ts:50`) |

A page commits in **one transaction**: records, links, chunks, the FTS rows, and the moved cursor, all or none (the rule `IIntentStore.CommitPage` already states, `IIntentStore.cs`). Embedding is a **separate, resumable job** (§5.6), so a slow embedder never holds a sync: a record is searchable by words the moment its page commits, and by meaning when its vectors land. A replay of a page changes nothing (compare `text_hash` and `version`). Sync runs as the engine, like `SyncSources` does today (`SyncSourcesService.cs`), and uses the same `SyncLocks`. A `sindri index sync [source]` verb and a built-in service run it; a source that is down is a row with its code, not a failed call (as now).

### 5.6 Chunking and embedding

`Chunker` (pure, `Aw.Context.Core`): clean, then window at 400 tokens with 40 overlap (`raw-window.ts:34-36`). ConnectAI counts tokens with `js-tiktoken` (cl100k) and says itself it is an approximation for the embedder's real tokenizer (`raw-window.ts:14-19`). Sindri adds no tokenizer package: it estimates a chunk's tokens as the larger of words times 1.3 and characters divided by 3.5, which overcounts dense text (base64, long urls: the case that broke ConnectAI's fixed 2,048-character window, `raw-window.ts:8-12`), and sends each batch to Ollama with `truncate: false` so an over-long input is an error, not a silent cut; on that error the chunk is halved and retried once (ConnectAI's "embed-fit backstop", `embed-fit.ts`). Whether `truncate: false` behaves as documented on the pinned Ollama is checked in phase I0.

`Embeddings` (tier 0): `IEmbedder.Embed(texts, ct)` against `POST {url}/api/embed`, loopback only (`127.0.0.1` or `[::1]`, never `localhost`), no proxy, no redirect, no cloud model, the model's digest checked against the pin and never downloaded: the rules of `IEmbedder` in the code index (`Aw.CodeIndex.Core/Ports/IEmbedder.cs:7-9`; code index spec §5.6). Batches of 32; the embed job works newest-updated first in chunks of 4,096 chunks until none is missing, and is restartable. **Cost** (derived from a cited figure, not measured): the code index spec expects a large repo (3,100 files, 41,000 symbols, `2026-10-19-sindri-code-index-design.md:625`) to take "an hour or two" of background CPU (`:860`), about 8 symbols a second. A symbol is a line or two; a chunk is up to 400 tokens, roughly ten times the text, so I assume 1 to 4 chunks a second on CPU, not 8. At that rate 50,000 chunks take 3.5 to 14 hours and 250,000 take 17 to 70 hours. A day's changes (a few hundred records, about a thousand chunks) are minutes. Apple silicon is faster (Ollama uses the GPU). Phase I0 measures the real rate; if it lands under 1 a second, Q6's smaller-model option comes back on the table.

### 5.7 LLM enrichment: the call is no

ConnectAI runs up to five model calls on every item after it is chunked: entity resolution, fact extraction (0 to 3 facts, up to 768 output tokens), an episode summary, procedure extraction and preference mining (`ING/ingest-gmail.ts:700-831`, inventory §3 step 6). On self-host the model is `qwen2.5:1.5b` through Ollama, in the cloud Claude Haiku through a gateway (`deploy/selfhost/.env.example`, inventory §1).

| | Cost for the owner's size (my arithmetic from the figures above; assumes 20,000 records) |
|---|---|
| Calls | up to 100,000 (20,000 x 5) on the first sync, then 5 per changed record |
| Time on CPU | a 1.5-billion-parameter model produces roughly 10 to 20 tokens a second on 16 cores; one call with a 300-token prompt and a 200-token answer is 10 to 20 seconds, so 100,000 calls are 12 to 23 days of CPU. Even at one call per record it is 2 to 5 days |
| Memory | a second model resident beside the embedder: about 1 GB of weights for the 1.5B model, more for a model good enough to extract facts reliably |
| Egress | the cloud variant sends every record's text to a hosted model: this reverses retrieval spec §3 (model egress is off unless the profile turns it on) |

What the owner gets back is entities, facts and summaries that Sindri's graph already produces another way: the rules linker makes deterministic links from the record text (retrieval spec D3), the optional model linker proposes links as untrusted `propose` rows (D15), and a feature's summary is the map's own `describe` step. ConnectAI needs its facts because it has no structure to hang records on; Sindri's map is that structure.

**Decision (I11): no model call on ingest, in v1.** Nothing in the store's design needs one: chunks and vectors are made from the scrubbed source text. If a later spec wants a one-line summary per record to embed beside the raw chunks (the "derived path", `chunk-synthesize.ts:1-23`), it is an opt-in job over records that have no summary yet, on the profile's model switch, bounded by a budget; the schema has room (`embedding.space` could gain `summary`) and nothing here blocks it. Q19.

## 6. The query path and ranking

### 6.1 Legs

Today's pipeline (retrieval spec §8.2): anchors, cascade, per-source candidates, per-source fusion, reciprocal rank fusion across sources (k = 60, `RankFusion.cs:13`), the feature boost, an optional rerank, the stale check, packing. The store changes step 3 and what a "source" is:

| Leg | Built from | Ranked by |
|---|---|---|
| **Dense** | the query (and each anchor name) embedded with the pinned model, scanned over `embedding` rows of `space = 'chunk'` with the caller's scope mask (§4.4), top 50 chunks, a relevance floor, then the best chunk per record | cosine, best first |
| **Lexical** | FTS5 over `chunk_fts` with each term quoted as a phrase, never as query syntax (`DocSql.Phrase`, `src/Context/Aw.Context.Infrastructure/Docs/DocSql.cs:47`), terms of a free-text query joined with `OR`, the same scope mask, top 50 chunks, best chunk per record | `bm25`, best first (`DocSql.cs:41`). ConnectAI's lexical leg is `plainto_tsquery`, which ANDs every term, so a long question matches almost nothing (`hybrid-search.ts`, inventory §5 note); `OR` with `bm25` does not have that failure |
| **Graph (anchors)** | exists today: records linked to the cascade of the query's anchors, ordered by cascade distance then edge type (retrieval spec §8.2 step 3) | as today |
| **Graph (seeds)** | new: the owner nodes of the top 7 dense and lexical hits (Sindri's choice; ConnectAI's seeds are the fused candidates' episodes, `recall.ts:164`), expanded by the same cascade, 2 hops, at most 50 neighbours per node, and only the 7 best-scored nodes carried to the next hop (ConnectAI's `HOP_LIMIT = 2`, `FAN_OUT_CAP = 50` and `INHIBITION_KEEP = 7`, which is a per-hop cut, `spreading-activation.ts:43-55`); the records linked to what that reaches and not already in a list | hops, then edge type |
| Code | exists today: git grep and the code index's neighbours | as today |

**The dense floor.** A nearest-neighbour scan always returns 50 rows, relevant or not, and a record at rank 1 of a leg of noise scores like one at rank 1 of a good leg. ConnectAI cuts the dense leg: everything within cosine distance 0.5 is kept, else the nearest 8 within 0.75, else nothing (`hybrid-search.ts:170-177`, applied at `:473`). Sindri does the same with its own numbers, because those two were tuned for a 1,024-dimension model and `nomic-embed-text` spreads its distances differently: the floor is two profile keys with defaults set from the backtest (phase I4), and a query whose dense leg is empty says so in the answer ("nothing close by meaning"), which is information, not an error.

Legs are fused exactly as sources are now: per leg, the best rank of a record across that leg's sub-queries; then reciprocal rank fusion across legs, `score = sum of 1 / (60 + rank)` (`RankFusion.cs:30-50`). Source scores are discarded; only ranks are kept (`SourceHit.cs`). A leg that is unavailable (Ollama down: no dense leg, `unavailable` with its code) drops out and the answer says so, as a degraded source does today.

### 6.2 Signals, and how they combine

`FeatureRanker.Apply` multiplies each fused score by `1 + weighted mean of the features` (`FeatureRanker.cs:23-27,41`), so features can at most double a score. The five existing features are scores from 0 to 1 (`RankFeatures.cs:12-55`): cascade distance (`1 / (1 + d)` from the query's anchors), edge type (decides 1, quotes 0.7, mentions 0.4, other 0.2), provenance (human 1, deterministic 0.7, model 0.4), supersession (1 stands, 0 replaced), and link recency (halves every 90 days from the day the link was written). Weights default to 1 each (`FeatureWeights.cs:10-22`).

Three are added. **Reach** is the graph signal: `1 / (1 + h)` where `h` is the fewest typed-edge hops from a seed's node to the record's node (0 when the record is itself a linked seed), and 0 for a record with no node (an unlinked record is not part of the graph, `IntentOverlay.cs:42`). **State** and **freshness** are record signals the store makes possible for every record, linked or not: state (open or no state 1, draft 0.75, merged or done 0.5, closed or canceled 0.5) and freshness (halves every 180 days from `updated_at`). New weights: reach 1, state 0.5, freshness 0.5. They are starting values, and (as D13 does for the reranker) they ship on only if a backtest arm with them beats the five-feature arm (§11); with the store off, the five-feature result is unchanged. Nothing here writes on read.

**Worked example.** Query: "why does split pane orientation reset on restore". The anchor is `feature:layout.split-pane.orientation`. Four records come back; ranks are within each leg (`-` = not in it):

| Record | Dense | Lexical | Graph | What the map knows |
|---|---|---|---|---|
| A: open Linear issue, "Orientation lost when restoring layout" | 3 | 1 | 1 | decides the feature; deterministic; linked 30 days ago; updated 10 days ago; a seed itself |
| B: repo doc chunk, `docs/layout.md` | 1 | 5 | - | not linked; doc updated 400 days ago |
| C: closed GitHub issue, similar words | 2 | 2 | - | not linked; closed, updated 800 days ago |
| D: merged GitHub PR, touches the feature's files | 4 | - | 2 | mentions the feature (cascade 1 from the anchor), deterministic, linked 200 days ago; two hops from A's node; updated 60 days ago |

Fusion, `1 / (60 + rank)` summed: A = 1/63 + 1/61 + 1/61 = 0.0487; B = 1/61 + 1/65 = 0.0318; C = 1/62 + 1/62 = 0.0323; D = 1/64 + 1/62 = 0.0318. Order by fusion alone: A, C, B, D.

Features (cascade, edge, provenance, supersession, link recency, reach, state, freshness), weights 1, 1, 1, 1, 1, 1, 0.5, 0.5 (sum 7):

- A: 1, 1, 0.7, 1, 0.794 (0.5^(30/90)), 1, 1, 0.962 (0.5^(10/180)): weighted sum = 5.494 + 0.5 + 0.481 = 6.475; mean 0.925; score 0.0487 x 1.925 = **0.0937**.
- D: 0.5, 0.4, 0.7, 1, 0.214 (0.5^(200/90)), 0.333, 0.5 (merged), 0.794: sum = 3.147 + 0.25 + 0.397 = 3.794; mean 0.542; score 0.0318 x 1.542 = **0.0490**.
- B: 0, 0, 0, 1, 0, 0, 1 (a doc has no state), 0.214: sum = 1 + 0.5 + 0.107 = 1.607; mean 0.230; score 0.0318 x 1.230 = **0.0391**.
- C: 0, 0, 0, 1, 0, 0, 0.5 (closed), 0.046: sum = 1 + 0.25 + 0.023 = 1.273; mean 0.182; score 0.0323 x 1.182 = **0.0381**.

Order after features: A, D, B, C. D jumped from last to second because the map links it to the feature; C, a near-identical text match nobody linked, fell from second to last. That reordering is the point of ranking with the graph, and the weights that produced it are what the backtest in §11 sets.

## 7. The map builds from the store

### 7.1 What changes for the build

`BuildMapService` (`Aw.Context.Features/BuildMap/BuildMapService.cs:21-28`, already at the seven-constructor-parameter limit) gets its doc and record input from one new record, `MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)`, the shape map-structure §11.2 gave `MapDocSources(IDocIndex Docs, ...)`. `IRecordStore` is a Context.Core port with a SQLite adapter in Context.Infrastructure; its read side is what the build and the search legs use, its write side is `Commit(RecordBatch)` for the sync driver.

- **The build sees every record in scope**, not the overlay's linked subset (`IntentOverlay.cs:42` today: "A record no rule linked, and no link names, is stored ... but is not part of the graph"). Doc records for every doc, cited paths from chunk text, directory docs: map-structure §11.2, unchanged in rule, with its input switched from `IDocIndex` to the store.
- **The build records which store state it read**: a `commit stamp` (the last committed sync page) in the draft's version row, so a build says "records as of stamp N" and the check line that §11.2 words as `docs: index is N commits behind` becomes `records: store is N pages behind` (numbers only, as §4.3 of that note requires). A stale source's edges are skipped, not guessed.
- **Source-stated links become edges.** A `record_link` of kind `closes`, `parent`, `blocks`, `duplicates`, `relates`, `attachment` whose two ends are stored records is a deterministic `record mentions record` edge, with the link kind as the edge's detail (`EdgeDetail.cs`); no new edge type in v1 (Q9). A `closes` from a PR to an issue, and the PR's files via the tracker half of #1373, are what let a lens on a feature show the issue, the PR that fixed it and the docs that describe it, from one graph.
- **The key is shared** (§4.3), so no mapping layer exists between what was searched and what is graphed.

### 7.2 What changes for #1373 (map-structure §11)

§11 of the map-structure note is written as **pending this spec** (a banner is added in this PR; its rules are otherwise kept):

| §11 says | Now |
|---|---|
| Doc records come from the build, from `IDocIndex` and the path list (§11.2) | From the build, from `IRecordStore` for the doc text and from the path list for existence. The rules (record globs, directory-doc names, home-feature links, cited-path `mentions`, roll-up) do not change |
| "The map build does not see it" (§11.1), the doc index is `doc_chunk` behind `IDocIndex` | The doc index **is** the store (`source_record` kind `doc`, `chunk`): `IDocIndex` and `SqliteDocIndex` are retired after `0017`; `RefreshDocsService` becomes the repo-docs feed of §5.5 |
| `MapDocSources(IDocIndex Docs, ICodeSymbolSource Symbols)` | `MapRecordSources(IRecordStore Records, ICodeSymbolSource Symbols)` |
| Open question 18: a doc's age needs a per-file commit time the reader doesn't give | Unchanged for docs (the repo head's time first). Tracker records now have real `created_at`, `updated_at`, `closed_at` |
| "The tracker half (issues, projects, decisions, freshness) is the ideas lane's own design" | The tracker half's inputs exist in the store: state, links, attachments and times for Linear and GitHub. The ideas lane designs only what a lens shows, not how records arrive |
| Overlay overlap: S7 deduplicates the overlay's edges (§11.2) | Unchanged |
| S7 needs S3 (§7) | S7 needs S3 **and** this spec's phase I1 (the `IRecordStore` port, its fake and its contract tests). The ideas lane can start S7 against the fake the day I1's port merges; it should not start before |

S3 (home features), S4 (roll-up), S5 (checks) and S6 (pins) do not touch docs or records and are unaffected.

### 7.3 Vectors of graph nodes (optional, phase I7)

`embedding` rows with `space = 'node'` hold a vector per feature and concept (its label plus its top member paths, re-embedded when that text's hash changes). Two uses, both proposals and never silent edges: a record that is similar to a feature but linked to none becomes a `propose` row in map review (model provenance, untrusted until accepted, retrieval spec D15, `RulesLinker`'s rules stay the deterministic path); and a query with no anchor can start from the nearest features. Whether I7 is worth building depends on how many tracker records stay unlinked after I5; the backtest reports that number (Q10).

### 7.4 Code

Code vectors stay in the code index's per-repo files (`embeddings` table, code index spec §4.2, `:163`), reached through the existing `Neighbors` (code index spec §7.1). A query therefore runs two nearest-neighbour passes, the store's and the code index's, fused as two legs. Moving code vectors into the store would make one pass but couples the code index's publish-by-pointer files to `context.db`; revisit only if the two-pass cost shows in the backtest (Q14).

### 7.5 Records and the build: the ideas lane's two constraints, and tracker records that touch code

The ideas lane set two constraints for doc records (map-structure §11.2). Does the store give a real reason to change either? No.

1. **Doc-record nodes come from the build's path list, keyed `record:repo:<repo>/<path>`.** Kept. The store has a `source_record` row per doc, but the node must not depend on the store having synced: a doc that matches the record globs and is in the path list gets its node even when the store has no row yet (then it simply has no cited paths and a `records: not yet synced` line in the report). The store adds the doc's *text*, not its existence. Tracker records are the other kind: they are not in any path list, so their node is `record:` plus their record key (`record:github:pr/<owner>/<repo>/<n>`, §4.3), and a record the store has but no rule or link has touched still becomes a node (§7.1).
2. **Cited paths become Deterministic `mentions` edges at 1.0 only on an exact path match; an unresolved citation is a count in the build report.** Kept, and the store makes the count better: the build reads chunk text for every record (not only docs), extracts backticked repo paths and `file:line` forms, and an edge exists only when the path is in the build's path list for that repo. Anything else (a renamed file, a path in another repo, a symbol) is counted by kind in the report's `citations` line: `citations: 212 resolved, 31 unresolved (19 missing, 7 other repo, 5 ambiguous)`. No fuzzy match, no basename match, no model.

**Tracker to code.** ConnectAI has no record-to-record or record-to-file links at all (inventory §4: "no table linking one episode to another"). The ones we add are source-stated, not inferred:

- A merged or open PR's changed paths: `GET /pulls/{n}/files` (§5.3) writes `record_link` rows of kind `touches` with `to_ref = 'path:<repo>/<path>'`. The build resolves each against its path list by exact match: a hit is one Deterministic `record mentions code` edge at 1.0, which the step-4 roll-up turns into `record mentions feature` with a count, so a lens on a feature shows the PRs that touched it. A path that no longer exists (a deleted or renamed file) counts as `missing` in the same report line.
- A PR that `closes` an issue and an issue's parent, blocks, duplicates, relations and attachments: `record_link` rows between records (§7.1).
- The result for the worked example in §6.2: A (the Linear issue) is `closes`-linked from D (the PR); D `touches` `src/layout/SplitPane.cs`, which is in the feature's files; so the feature, the issue, the PR and the doc that cites the file are one connected neighbourhood, with no model involved.

Cost: one list call per PR for files, 100 per page. 5,000 PRs are 5,000 to 10,000 calls, about two hours of GitHub's 5,000-an-hour budget, spent over the backfill.

## 8. Onyx: removal and migration

### 8.1 What goes, and what stays

Counts are files that mention "onyx" (an upper bound on what is deleted; `grep -rli onyx` over the tree) and are rechecked when each PR is cut.

| Area | What | Action |
|---|---|---|
| CLI | `sindri onyx up/down/status/open`, `OnyxVerbs.cs`, `CommandTable.cs:104,167`, `Interrupt.cs:45`, the status row `Verbs.cs:135` | Delete. `onyx down` stays one release as a stub that stops a leftover stack and prints the Docker command to delete its volumes (Q8) |
| API and setup | `ConnectOnyx`, `DeferOnyx`, `SetupOnyx`, `OnyxAccount`, `OnyxEnsure`, `OnyxStatus`, `OnyxUp`, `OnyxDown` slices; the endpoints `ConnectOnyxEndpoint`, `DeferOnyxEndpoint`, `SetupOnyxEndpoint`; the Onyx parts of `GetSetup` and `SetupSources`; the serve hooks `OnyxServeHooks.cs` | Delete. The setup screen's Onyx step is replaced by "connect your sources" (I8) |
| Sindri Core and Infrastructure | `Aw.Sindri.Core/Onyx/*` (about 50 files: compose, container runtime, provisioning, pins, login), `Aw.Sindri.Infrastructure/Onyx/*` (the Docker CLI runtime, HTTP provisioning, compose YAML, health), 93, 57, 34 and 23 files in Core, Features, Infrastructure and Host mention it | Delete; keep `KeychainSecretWriter` and `ISecretWriter` (§5.4) |
| UI | 20 files in `Aw.Sindri.Ui` | Delete the Onyx views and view models; the setup flow and its goldens change |
| Context | `Aw.Context.Core/Onyx/*`, `Aw.Context.Infrastructure/Onyx/*` (23 files), `OnyxSection`, `ContextSettings.Onyx` (`ContextSettings.cs:48`), `OnyxSources` in `ContextSourceCatalog.cs:34-37`, `Eval/OnyxBriefSource.cs`, the Onyx arm of the backtest (`Eval/Backtest/Arms.cs`), `Permalinks`' loopback Onyx origin | Delete after the backtest brief source reads the store (I5) |
| Doctor | `onyx:stack` (`RunDoctor/OnyxStackDoctor.cs:20`), `context:onyx` (`OnyxVersionProbe.cs:10`), the aot-smoke expected output | Delete the rows; update `src/Sindri/aot-smoke/expected.txt` and run aot-smoke in that PR |
| Errors and profile | `SND-ONYX-*` (`SindriErrors.Onyx.cs`), `docs/sindri/errors.md`, profile keys `context.sources.onyx.*` | Codes retired. A profile that still has the key is **an error** for one release naming the fix, not silently ignored (Q15). Removing the key changes the profile's hash, so the owner re-approves once (`sindri profile approve`) |
| Pins and scripts | `context/onyx.lock`, `scripts/onyx-lock.sh`, `scripts/install-onyx.sh`, `scripts/tests/install-onyx.test.sh`, `scripts/tests/onyx-lock.test.sh`, the Onyx step in `setup.sh` (flags `:142-153`, step `:595-605`) and its `--no-onyx` / `--with-sindri` wording, the `deploy/images.lock` note (`:34`), AGENTS.md's command list and the merge-gate line for `onyx-lock.sh` | Delete; `--with-sindri` stays accepted and does nothing (as AGENTS.md says it already does); `--no-onyx` stays accepted for one release |
| Docs | `docs/sindri/onyx.md`, `SETUP.md`, `first-context-run.md`, `docs/csharp/sindri*.md` Onyx sections, `.env.example` (any `AW_*ONYX*` the code reads) | Rewrite or delete; `scripts/check-env-example.sh` in the gate. Old specs stay as history, each gaining a one-line "superseded by 2026-10-27" |
| Tests | `tests/Aw.Sindri.Tests/{OnyxUp,OnyxAccount,OnyxStatus,SetupOnyx,OnyxEnsure,OnyxDown,OnyxPinsTests}`, `tests/Aw.Context.Tests/Onyx`, `FakeOnyxServer` in `Aw.Context.Testing/Fakes`, the API goldens (`tests/Aw.Sindri.Tests/Golden`, 37 files; `Aw.Sindri.Ui.Tests/Golden`, 8) | Delete with their code; regenerate goldens in the same PR |

### 8.2 Order (so main stays green and the backtest keeps a baseline)

1. Build the store, the feed driver, GitHub, Slack and Linear feeds, and the new legs with Onyx still in place (I1 to I4).
2. Run the backtest on both: Onyx arm and store arm, same corpus, same questions. Parity (store arm at least as good on the pass bars of retrieval spec §9.4) is the gate to delete.
3. Switch the default: sources in the catalog are the store's; Onyx is no longer built into the catalog.
4. Delete in the order of §8.1's table, top to bottom, one PR per row group (each PR builds and tests alone). The `onyx.lock` and setup step go last so a box that never ran `onyx up` is untouched at every step.
5. Owner actions, once, listed by `sindri setup` as one click each: paste the GitHub and Slack tokens; approve the profile; optionally `docker compose -p sindri-onyx-<hash> down -v` and delete `$AW_SINDRI_HOME/onyx/` (Sindri never deletes the containers or volumes itself; `sindri doctor` warns while they exist).

### 8.3 What happens to data Onyx already gave Sindri

Less than it sounds: Sindri never stored Onyx's text (§0). What it stored is in `intent_record`, `intent_link`, `intent_decision`, `intent_edge` and `intent_conflict` (`0006`, `0007`, `0008`, `0009`, `0011`), plus human overrides in `map_override`.

| Stored thing | Key before | Key after | Result |
|---|---|---|---|
| A Linear issue | `linear:issue/<uuid>` | the same | Nothing to do. The native adapter and Onyx's connector already wrote the same key (`LinearRecordKeys.cs`, `OnyxRecordKeys.cs:44-47`). `source` changes from `onyx` to `linear` when the Linear feed first rewrites the row |
| A Slack thread | `slack:<channel>/<ts>` | the same | Nothing to do (§5.3) |
| Other Onyx documents | `onyx:<type>/<id>`, including GitHub as `onyx:github/<id>` | GitHub: `github:issue\|pr/<owner>/<repo>/<n>`; others: none | Onyx's GitHub id "is whatever Onyx gives it" (`docs/sindri/onyx.md:183`), so a mapping is not guaranteed. Rows that can't be mapped become orphaned links (the overlay already reports them, `IntentOverlayResult`); their links are **derived** by rules from record text and are rebuilt from the new GitHub records at the next sync. **Human** decisions on them (overrides naming `record:onyx:...`) can't be re-derived: the migration lists them before it drops anything, and the owner decides (§13, Q8) |
| Repo docs | `doc`, `doc_chunk` | `source_record`, `chunk` | Copied by `0016`, scrubbed text as it was; vectors by the embed job |
| Onyx's own index (its Postgres and OpenSearch volumes) | | | Not migrated. It is a derived copy of data the connectors read again. Re-reading is the cost: GitHub about 200 list requests per 20,000 records (§5.3) |

## 9. Packages, tiers and edges

### 9.1 The rules that decide this

Project and compose edges point strictly down the tiers; a tier-1 package cannot reference another tier-1 package; Core and Infrastructure may use tier-0 packages only; another package is reached through its Core only (`deps.yaml:24-60`; `.agents/rules/csharp.md:56,83-85`). So connectors cannot sit beside Context and call its `SourceRecord`, and the code index and Context already meet only in Sindri's Host. The glue pattern exists; this spec uses it.

### 9.2 New packages and edges (`deps.yaml`)

```yaml
packages:
  - id: Embeddings
    kind: package
    tier: 0
    sublevel: 1                      # references Aw.Kernel
    projectPrefix: Aw.Embeddings
    src: src/Embeddings
    note: "the loopback Ollama embed client (digest pin, no proxy, no redirect), the float32 vector codec, L2 normalisation and the top-k scan (IVectorScan); domain-free; BCL only (HttpClient, System.Numerics)"
  - id: Connectors
    kind: package
    tier: 1
    projectPrefix: Aw.Connectors
    src: src/Connectors
    note: "source readers (GitHub, Slack; later Notion, Jira): the IConnector contract, text cleaner, HTTP clients with a host allowlist; a port of ConnectAI's connector mechanics; no storage, no Context types"
edges:
  - { from: Embeddings,   to: Kernel,          kind: project, status: planned, note: "Result, IService, ids" }
  - { from: Connectors,   to: Kernel,          kind: project, status: planned, note: "Result, ids, IService" }
  - { from: Connectors,   to: KernelTelemetry, kind: project, status: planned, note: "logs and spans (codes and counts only, never record text)" }
  - { from: Context,      to: Embeddings,      kind: project, status: planned, note: "Context.Infrastructure: the query embedding, the vector scan, the embed job's client" }
  - { from: CodeIndex,    to: Embeddings,      kind: project, status: planned, note: "phase I6: OllamaEmbedder and VectorCodec move here (until then CodeIndex keeps its own)" }
  - { from: Sindri,       to: Connectors,      kind: project, status: planned, note: "Features and Host use IConnector and its records through Aw.Connectors.Core" }
  - { from: Sindri,       to: Connectors,      kind: compose, status: planned, note: "Host calls AddConnectorsFeatures and AddConnectorsInfrastructure and nothing else from them" }
  - { from: SindriUi,     to: Sindri,          kind: http,    status: current, note: "unchanged; the setup flow's API routes change (Onyx routes removed, source connect routes added)" }
```

Only `Aw.Context.Infrastructure`, `Aw.Connectors.Infrastructure` and the Host use these tier-0 packages: Core and Features may use `Aw.Kernel` among tier 0 and nothing else (`deps.yaml:37-60`, ARCH-DIR-005), which is why the chunker and the ports sit in Core and the scan and the client do not. Packages `Context` and `Sindri` keep their present edges (`deps.yaml:421-436,507-514,629-636`). No package gains a new edge to `Context`. Nothing in `deps.yaml` names Onyx (its executables are external, as the managed-Onyx spec §9 notes), so removal edits no edge.

Why not put the connectors inside Context, as Linear and Onyx sit today (`Aw.Context.Infrastructure/Linear`, `/Onyx`)? That works under the tier rules with no adapter and keeps one contract (`IContextSource`). It was weighed and not chosen because Context is the retrieval engine and 58 of its files already mention Onyx (`grep -rli onyx src/Context`, 2026-10-27), and the owner asked for ConnectAI's code to be pulled out into something Sindri owns, which a package with its own contract and tests makes visible and reusable (a recipe block can use `Connectors` without Context). The cost is the adapter in the Host (one small class per connector) and two places to read when tracing a record. Q5 puts the choice to the owner.

### 9.3 Where the code goes (by the C# rules)

| Package | Layer | What |
|---|---|---|
| `Aw.Embeddings` | library | `IEmbedder`, `OllamaEmbedder`, `VectorCodec`, `IVectorScan` with `ScanInProcess`; fakes and contract tests in `Aw.Embeddings.Testing` |
| `Aw.Connectors.Core` | Core | `IConnector`, `ConnectorPage`, `ConnectorItem`, `TextCleaner`, the profile records `GitHubSettings`, `SlackSettings` (a secret pointer and scope lists, with `Create` that validates, as `LinearSettings` does) |
| `Aw.Connectors.Features` | Features | `PullGitHub`, `PullSlack` slices (a request, a service, a fake client in tests) |
| `Aw.Connectors.Infrastructure` | Infrastructure | the HTTP clients, wire records, source-generated JSON context |
| `Aw.Context.Core` | Core | `IRecordStore`, `RecordBatch`, `RecordFeed`, `Chunker`, `RankFeatures` additions, `ReachSignal` |
| `Aw.Context.Features` | Features | `SyncSources` rewritten around feeds, `EmbedRecords`, the search legs, `BuildMap` reading `IRecordStore` |
| `Aw.Context.Infrastructure` | Infrastructure | `SqliteRecordStore` (migration `0016`), `VectorMatrixCache`, the Linear feed |
| `Aw.Sindri.Host` | Host | the adapter `ConnectorFeed : RecordFeed` over `IConnector`; `sindri index connect\|sync\|status\|purge` |
| `Aw.Sindri.Features` | Features | the `index-sync` and `index-embed` built-in services (the shape of `code-index-embed`) |

## 10. Security and trust

- **Trust is unchanged** (retrieval spec §7): record text, titles and authors are `untrusted` and fenced on egress; stored text is scrubbed first. A new stored-text surface is why Q7 asks the owner's yes before the first line of code.
- **Egress.** Nothing leaves the machine except the connectors' own requests to their tool's API, over `https` to an allowed host with no redirect to another host (the forge adapter's rule, `src/Forge/Aw.Forge.Core/Ports/IForge.cs:9`), and the loopback embed call. A test asserts both.
- **Callers.** Reads are any caller; `index sync` is any caller (it runs as the engine, as `sources sync` does); `index connect` writes a secret and is human-only; `index purge <source>` deletes a source's rows and vectors and is a `desk` action (one reversible-by-resync step, the terminal or the app may run it).
- **Prompt injection in a record** is the existing threat (S-H3): record text never reaches a model on ingest (§2), and on egress it is fenced.
- **Direct messages and unlisted channels are never read**: the Slack connector refuses a channel not in the profile before sending a request (`SND-CONTEXT` code, a test with a fake Slack that fails the run on any other route).
- **Disk.** The store holds more private text than any file Sindri has held; it is `0600` in a `0700` directory, and `sindri doctor` warns when the file's mode or its directory's is wider (the check the three SQLite files get today).

## 11. Proof

- **Unit:** `Chunker` (windows, overlap, dense text, multi-byte at a cut: the cases ConnectAI's tests cover, `connections/src/connectors/__tests__`), `TextCleaner`, the cursor boundary rule, the reach and state and freshness scores, fusion with the new legs, the scope mask inside the scan (a session with three allowed records gets three, not zero).
- **Contract:** `IRecordStore`, `IConnector` and `IEmbedder` each get a fake and a contract suite the real adapter also passes (the repo's rule, `.agents/rules/csharp-testing.md`).
- **Integration:** `SqliteRecordStore` on temp files: migration `0016` run on a copy of a database made by `0015` (docs copied, intent tables untouched, old `source_cursor` rows kept); a page commit is atomic (kill between records and cursor: neither moves); a replayed page writes nothing; a ruleset change marks rows outdated and a re-pull fixes them; FTS and a dense query agree on a planted record.
- **Fakes:** a fake GitHub server and a fake Slack server (`Aw.Connectors.Testing`), the way `FakeOnyxServer` is built today; a fake Ollama.
- **Heavy (nightly):** real Ollama with the pinned model: dense leg ranks a known near-duplicate first; the measured numbers of phase I0 (scan p95 at 50,000 and 250,000 synthetic chunks, embed throughput, store size).
- **Backtest (the gate for deleting Onyx, §8.2):** the existing arms plus a `store` arm and a `store+features` arm on the owner's three Linear projects' corpus and questions (retrieval spec §9); the report prints, per arm, the pass-bar numbers and the share of tracker records left unlinked (input to Q10).
- **Setup proof:** `./setup.sh --dry-run` shows no Onyx step; `scripts/check-env-example.sh`, `node scripts/check-deps.mjs`, `scripts/dotnet-verify-packages.sh` pass; `sindri doctor` lists the new rows (`index:store`, `index:embeddings`, `index:github`, `index:slack`) and none for Onyx.

## 12. Phases and parallel plan (for /specToProvenPR)

Lanes: api, engine, context, ui, ideas. Builders are Sonnet, in parallel, one project each (builders never build or test beyond one light build; leads run the gates). Every phase's PR carries its own tests.

| Phase | What | Needs | Lane |
|---|---|---|---|
| **I0: measure** | A throwaway harness, never merged: the scan's p95 at 50,000 and 250,000 chunks on this box (float32 and int8); Ollama `truncate: false`; embed throughput; sqlite-vec's load on the bundled `e_sqlite3` and under NativeAOT; one Slack `conversations.history` call with the owner's token to read its page cap and `Retry-After`; the owner's real record counts from the Linear and GitHub list endpoints. Results written into this spec's §4.4 table | nothing | context |
| **I1: store and port** | Migration `0016`, `SqliteRecordStore`, `IRecordStore`, `RecordBatch`, `Chunker`, the doc copy; `0017` drop in a later release. The `IRecordStore` fake and contract land first so I2 and the ideas lane start on it | I0 for the budget only | context |
| **I1e: `Embeddings`** | The tier-0 package: `IEmbedder`, `OllamaEmbedder`, `VectorCodec`, `IVectorScan`; its `deps.yaml` entries | nothing | engine |
| **I2: feed driver and Linear** | `RecordFeed`, `SyncSources` rewritten, the Linear feed with issue state, `EmbedRecords` job and `index-sync`/`index-embed` services, `sindri index sync\|status` | I1, I1e | context |
| **I3: GitHub** | `Connectors` package, GitHub client, settings, the Host adapter, `sindri index connect github`, Permalinks host | I1 (the port), fake server | api |
| **I3b: Slack** | The Slack client, settings, `index connect slack` | I3 (the package and adapter exist) | api |
| **I4: search legs and ranking** | Dense and lexical legs over the store with the scope mask, the seed graph leg, `Reach`/`State`/`Freshness`, `FeatureWeights` additions, backtest arms | I1, I1e, I2 | context |
| **I5: parity run, switch, backtest brief from the store** | The backtest brief source reads the store; the parity run; the catalog's default switches. **The gate to I8** | I2, I3, I3b, I4 | context |
| **I6: `CodeIndex` uses `Embeddings`** | Move `OllamaEmbedder` and `VectorCodec`; optional, can wait | I1e | engine |
| **I7: node vectors and similar-link proposals** | `space = 'node'`, proposals in map review. Only if I5's unlinked share justifies it (Q10) | I5 | context, ideas |
| **I8: Onyx removal** | §8.1 in the order of §8.2: verbs and slices, then Sindri Core and Infrastructure, UI, Context, doctor and aot-smoke, errors and profile, pins and scripts and setup, docs, tests and goldens | I5 | engine (Sindri side), ui (views and goldens), context (Context side), api (endpoints) |
| **S7 (map-structure §11)** | Doc records and the tracker half of #1373 | S3 and I1's port and fake | ideas |

I0, I1 and I1e start together (three builders). I2 starts when I1's port and fake merge; I3 can start on the same day against the fake. I4 starts when I1 and I1e merge, with a fake feed. I5 waits for I2 to I4. I8's PRs are independent after I5 and run in parallel by area; the ui lane's goldens and the engine lane's aot-smoke expected output are the shared files, so those two PRs merge in turn. Shared files with the map lane: `BuildMapService.cs` (S7's wiring) and `EdgeRules.cs` (no change here: Q9 adds no type).

### 12.1 Work units (builders, Sonnet, one project each)

Every unit is a red test plus its code in one project, so the lane lead can run many at once. U-numbers are for this table only.

| Unit | What | Project | After |
|---|---|---|---|
| U1 | `VectorCodec`, L2 normalise, `IVectorScan` + `ScanInProcess` (float, `Vector<float>`), fake and contract suite | `Aw.Embeddings` | none |
| U2 | `OllamaEmbedder` (loopback guard, digest check, batches, `truncate:false`), fake Ollama | `Aw.Embeddings` | none |
| U3 | `deps.yaml` entries, `check-deps` pass, scaffold of the two packages | repo root | none |
| U4 | Migration `0016`, schema, `SqliteRecordStore` (commit page, read, tombstone) | `Aw.Context.Infrastructure` | U3 |
| U5 | `IRecordStore`, `RecordBatch`, fake and contract suite | `Aw.Context.Core`, `.Testing` | none |
| U6 | `Chunker`, `TextCleaner` (pure) | `Aw.Context.Core`, `Aw.Connectors.Core` | none |
| U7 | Doc copy `doc`/`doc_chunk` to the store; repo-docs feed | `Aw.Context.*` | U4, U5 |
| U8 | `RecordFeed`, rewritten `SyncSources` driver, phases and cursor columns | `Aw.Context.Features` | U5 |
| U9 | Linear `Pull` + issue state + new list query | `Aw.Context.Infrastructure` | U5 |
| U10 | `EmbedRecords` job, `index-embed` service, `VectorMatrixCache` | `Aw.Context.*`, `Aw.Sindri.Features` | U1, U2, U4 |
| U11 | GitHub client, cursor boundary, rate limit, fake server | `Aw.Connectors.Infrastructure`, `.Testing` | U3, U6 |
| U12 | GitHub settings, `PullGitHub` slice, Host adapter, Permalinks host | `Aw.Connectors.Core/Features`, `Aw.Sindri.Host` | U11, U5 |
| U13 | Slack client, thread rules, rate limit, fake server | `Aw.Connectors.Infrastructure` | U11 |
| U14 | `sindri index connect|sync|status|purge` verbs and `index-sync` service | `Aw.Sindri.Host`, `.Features` | U8, U12 |
| U15 | Dense and lexical legs with scope mask, dense floor | `Aw.Context.Features/Infrastructure` | U4, U10 |
| U16 | Seed graph leg, `Reach`, `State`, `Freshness`, weights | `Aw.Context.Core` | U5 |
| U17 | Backtest `store` and `store+features` arms; brief source from the store | `Aw.Context.Infrastructure/Eval` | U15, U16 |
| U18 | `MapRecordSources`, build reads the store, citation counts, `touches` edges, commit stamp line | `Aw.Context.Features` | U5, S3 |
| U19-U26 | Onyx removal PRs, one per row group of §8.1 (CLI, API/setup, Sindri Core/Infra, UI, Context, doctor/aot-smoke, errors/profile, pins/scripts/setup, docs, tests/goldens) | per area | I5 |

U1 to U3, U5 and U6 start on day one (none depends on another): five builders. U4 follows U3. U9, U11, U16 start once U5 and U6 merge. The lane leads run the gates; builders do the one light build of their project.

### 12.2 Box cost, in one place

| What | Number | Where it comes from |
|---|---|---|
| Disk, Onyx removed | frees up to 11.26 GB (`context/onyx.lock:61`) of images and volumes the owner deletes by hand (§8.2) | cited |
| Disk, store | 0.3 GB at 50,000 chunks, 1.6 GB at 250,000 (§4.4) | arithmetic |
| Memory, Onyx removed | frees 5.9 GB of Docker memory (`context/onyx.lock:62`) | cited |
| Memory, store | 154 MB (50,000 chunks, float32) to 768 MB (250,000) for the vector matrix in `serve`; int8 above 100,000 chunks is a quarter | arithmetic |
| CPU, first embed | 1 to 4 chunks a second: 3.5 to 14 hours for 50,000 chunks, in the background, newest first | derived from a cited figure (§5.6), measured in I0 |
| CPU, a query | 8 to 77 ms for the scan, plus one query embed (tens of ms to a second on CPU) | arithmetic, measured in I0 |
| Network | GitHub about 200 list requests per 20,000 records plus 5,000 to 10,000 file lists; Slack depends on its rate limit (§5.3) | cited and arithmetic |
| Model on ingest | none (§5.7) | decision |

Gates that stay sequential: any PR that edits a recipe or the shipped catalog runs the Sindri integration lane; any PR that changes a doctor row updates `src/Sindri/aot-smoke/expected.txt` and runs aot-smoke; any PR that adds a `using Aw.` or a Core sub-namespace runs `Aw.Architecture.Tests`; any new environment variable goes in `.env.example`.

## 13. Open questions for the owner (plain words)

1. **One file or two?** The index goes into the same `context.db` as the graph so a record, its chunks and its node join in one query. The price is one bigger file. *Recommend the same file; a separate `index.db` only if the file passes about 5 GB, where `VACUUM` starts to hurt.*
2. **Vector search: scan it, or add sqlite-vec?** I recommend scanning in process (nothing new to install), with a budget of 150 ms at your real size, and sqlite-vec or an index structure only if phase I0 or later numbers miss it. *Recommend the scan, budget as stated, decided by I0's measurement.*
3. **GitHub issues as real records?** On 2026-10-23 you decided GitHub issues stay search-only context with no Sindri adapter, because Onyx brought them in (`docs/sindri/onyx.md:183-186`). With Onyx gone, Sindri's own GitHub connector has to read them. *Recommend: yes, as records with a state and links (kind `issue` and `pr`), searchable and linked into the map, but still never turned into a Sindri item automatically; `sindri item add` stays the only way.*
4. **Which connectors after GitHub, Slack and Linear?** ConnectAI has Notion, Jira, Granola, Zoom. Onyx gave you Confluence and Drive spaces; ConnectAI has neither. Do you use any of them? *Recommend: stop at GitHub, Slack and Linear; add Notion or Jira only when you name one you use. Docs spaces without a ConnectAI equivalent wait until you need one.*
5. **Connectors as their own package, or inside Context?** *Recommend a new tier-1 `Connectors` package with one small adapter per connector in the Host (§9.2): it is the visible "ConnectAI code, Sindri owned" piece. If you'd rather have one contract and no adapter, inside Context works with no rule broken.*
6. **Which embedding model?** The box already has `nomic-embed-text` (768 dimensions, pinned in `context/code-index.lock`) for the code index. ConnectAI used a 1,024-dimension model. *Recommend nomic: one model for code and records, no new pull. Every vector row names its model, so a later change is a re-embed job, not a migration.*
7. **Is storing scrubbed record text OK?** Today Sindri keeps no record text; it asks Linear or Onyx each time. The index needs the text on this box. It is scrubbed first, in a `0600` file, and a purge verb exists. This reverses a line of the retrieval spec, so it needs your yes. *Recommend yes, with scrub-then-store, `blocked` records kept without text, and no retention limit (closed records stay: it is an index of your own tools, not a SaaS retention problem).*
8. **When does Onyx go, and what of the leftovers?** *Recommend: after the parity backtest (I5), not before; `sindri onyx down` stays one release as a stub that stops a leftover stack and prints the Docker commands to delete its volumes, and Sindri never deletes your containers or volumes. Human decisions that point at `onyx:` keys are listed before anything is dropped.*
9. **Edges between records.** A PR that closes an issue becomes a `mentions` edge with the link kind as its detail, not a new edge type. *Recommend that for now; a `closes` type is one row in `EdgeRules` if lenses want to show it differently.*
10. **Do we build node vectors and similar-link proposals (I7)?** *Recommend not yet: I5 reports how many tracker records stay unlinked after the rule linker runs; build I7 only if that share is over 20%.*
11. **Weights for reach, state, freshness.** Starting values 1, 0.5, 0.5. *Recommend they ship only if the backtest arm with them beats the five-feature arm on the pass bars, as the reranker does (D13); otherwise they stay at zero.*
12. **State words.** Six: open, closed, merged, draft, canceled, done; the source's own word is kept beside. *Recommend that; Linear's "In Review" and GitHub's draft PR both fit.*
13. **`intent_record` and `source_record` repeat a title, version and aliases.** *Recommend leaving both in v1 and folding `intent_record` into `source_record` after Onyx is gone and the dust settles.*
14. **Code vectors: leave them in the code index?** A query then runs two scans. *Recommend leaving them, and revisiting only if the backtest shows the two-pass cost or a worse fusion.*
15. **A profile that still names Onyx after removal:** an error with the fix, or ignored with a warning? *Recommend an error for one release; a silently ignored `onyx` key would look like it still worked.*
16. **Slack: which token, and which channels, and what kind of app?** ConnectAI's Slack reader is throttled to 1 request a minute and 15 messages a call (`slack/transport.ts:13-15`), the limit Slack puts on apps distributed outside its Marketplace; as I understand it an app you create in your own workspace is not limited that way (not verified; I0 checks with your token). *Recommend a bot token from an internal app you create and install yourself, channels listed by name in the profile, no direct messages ever, private channels only if listed and the bot is a member. If your workspace turns out to be throttled, Slack stays a slow background source and nothing else waits on it.*
17. **Any ConnectAI file copied as it is?** The plan reimplements everything in C# with ConnectAI as a guide, and copies nothing (it is BSL 1.1 and yours). *Recommend that; say which file if you want one lifted.*
18. **First embed takes hours on CPU.** Newest records first, resumable, searchable by words the moment they sync. *Recommend accepting that, with the embed job working newest first and capped by the profile's `context.index.maxChunks` (default 60,000) so the first full embed is bounded to about a day; the newest records are searchable by meaning within the first hour.*
19. **Any model call on ingest?** ConnectAI makes up to five per item (summary, facts, entities, procedures, preferences). For your size that is 2 to 23 days of CPU and a second resident model, or egress to a hosted model (§5.7). *Recommend none in v1; the graph's own linker does the useful part. A one-line summary per record is a later, opt-in job if search quality asks for it.*
20. **A cap on how much to embed?** `context.index.maxChunks`, default 60,000, newest records first. Past it, older chunks are searchable by words only. *Recommend the cap; raise it when I0's measured embed rate says the box can afford more.*

## 14. Where the code differs from what you'd assume

1. **Sindri keeps no Onyx data to migrate.** The intent tables store keys, titles and links, never text (`0006_s5b_intent_links.sql:2`). Onyx's index is a copy of what the tools hold. "Migrating Onyx" is a key mapping (§8.3) and a re-read, not a data copy.
2. **The source contract is already a feed in name only.** `IChangeFeed` exists (`IChangeFeed.cs:6`) and nothing implements it; `IntentSyncRun` finds records by searching with map terms (`IntentSyncRun.cs:134`). The new driver is a different design, not an activation of the old one.
3. **Linear is further along than ConnectAI's.** Sindri's native adapter reads projects, documents, updates, attachments and relations (`LinearIssue.cs:3-17`, `Operations/`); ConnectAI's reads issues and comments (`linear/SPEC.md`, `transport.ts`). The one gap is issue state: the query asks for it (`Issue.graphql:4`) and the record drops it. So Linear is the one connector that is **not** ported.
4. **ConnectAI keeps no structured records.** Its own rule: "ConnectAI deliberately does not own a normalized per-vendor record mirror" (`CLAUDE.md:16`). Its GitHub connector turns a PR's state, priority, blockers and counts into one synthesized sentence (`github-connector.ts:87-96`) and keeps only that sentence, the raw text windows and a hash of the participants (`:155-167`). The structured `source_record` and `record_link` tables are new design; what is ported is the fetching, the paging, the cursors and the chunker.
5. **ConnectAI's ranking writes on read.** Its recency re-rank bumps an access count on every retrieved chunk (`actr-rerank.ts`, "WRITE-BACK"). A ranking that changes with how often you asked can't be backtested, so it is not ported (§5.1).
6. **The code index's vector loop is too slow for this.** It is a scalar loop in `double` with three accumulators per pair (`VectorCodec.cs:42-50`), fine for one repo's symbols and about an order of magnitude off what 250,000 chunks need (§4.4). The store's scan is new code in `Embeddings`.
7. **Tier rules, not taste, decide the package split.** A tier-1 package can't reference another tier-1 package and Core and Infrastructure may use tier 0 only (`deps.yaml:24-60`), so `Connectors` can't speak Context's types; Sindri's Host is the glue, as for the code index (`ContextFeaturesServiceCollectionExtensions.cs:95`). The embedder had to go to tier 0 for Context's query path and the connectors alike.
8. **Docker stays on the box.** Serena runs in a container (`setup.sh:268-290`), so removing Onyx does not remove Docker from setup. What goes is Docker as a requirement of Sindri.
9. **The backtest still depends on Onyx.** The eval arms read Onyx for the intent brief (`Eval/OnyxBriefSource.cs`, `Eval/Backtest/Arms.cs`), which is why parity is run before deletion (§8.2) and not after.
10. **A doc's key and a node's key are already one thing.** Repo docs are `repo:<repo>/<path>` and their node `record:repo:<repo>/<path>` (`RepoDocs.cs:17`, `IntentOverlay.cs:123`). "Ids shared with the graph" is mostly extending that rule to chunks and vectors, not a new scheme.
11. **Slack keys already match.** Onyx's Slack connector writes `slack:<channel>/<ts>` (`OnyxRecordKeys.cs:48-52`); our connector writes the same, so stored Slack links need no migration. GitHub is the one that doesn't map (§8.3).
12. **Permalinks allow one host today.** `https` on `linear.app` (`Aw.Context.Core/Trust/Permalinks.cs:14`); a result's GitHub and Slack links are dropped until I3 adds their hosts.
13. **Slack's reader was read in code, and it is rate-limited by design.** The inventory has Slack as spec-level only. Its transport (`slack/transport.ts`) is 304 lines and assumes Slack's non-Marketplace limits, 1 request a minute and 15 messages per call (`:13-15,25-26`). Whether that binds an app the owner creates in their own workspace is unverified (§5.3, Q16).
14. **ConnectAI re-embeds a whole item whenever it changes, with no hash check.** `clearEpisodeChunks` then re-insert (inventory §3 step 5, a guess that unchanged items are skipped only because incremental reads return changed ones). Sindri's `text_hash` per record and per chunk is new (§4.2) and is what makes a reconcile run cheap.
15. **ConnectAI's lexical leg ANDs every term.** `plainto_tsquery` needs all words, so a long question finds little (inventory §5). Sindri's `OR` with `bm25` is a deliberate change (§6.1).
16. **Embed speed was quoted for symbols, not chunks.** The code index's "hour or two" is for short symbols; a 400-token chunk is far longer, so the first full embed is slower per item than the code index's figure suggests (§5.6).
17. **ConnectAI's GitHub reader cannot link anything.** No comment text, no review text, no cross-references, no permalink in the breadcrumb (`github-connector.ts:11`, `github/SPEC.md` §5); its `/pulls` list is asked for change counts it probably does not return (`transport.ts:340-343`, inventory open question 2). Every link in §7.5 is new work, not a port.
