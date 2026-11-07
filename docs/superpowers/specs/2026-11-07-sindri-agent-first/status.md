# Sindri agent-first: status

Issue: jthorare/agentic-workflow#2258. Generic only (no workplace names; grep-checked).

## Surfaces
- Hand map from origin/main: profile approval (ProfileDiff, ApproveProfile), SND-AUTH-002 (89 sites, 59 files under src/),
  recipe engine (RecipeWorkflow, registry, line dispatcher, station blocks), agent runtime P8 (launch, complete, recovery),
  sandbox host groups (docs/csharp/sindri-sandbox.md:179), ModelRouting (Aw.Context.Core), code-index embed model pin.
- PR cards: one read-only researcher mapped each PR to path:line (prs/pr-01..18.md).
- No Sindri context map query (the sindri MCP isn't registered for this repo in this session).

## Counts
89 SND-AUTH-002 sites / 59 files (grep over src/*.cs, excluding obj/).

## Decisions (owner, 2026-11-07, in the owner's words where quoted)
D1 drop approvals (profile + workflow) · D2 agents get every verb ("the agents are good enough to test it out with me first")
· D3 remove recipes entirely, drop the pipeline ("it doesn't work") · D4 agent-first via MCP, app optional
· D5 generic triggers · D6 catch-up only, no webhooks · D7 pull handoff inbox · D8 connection list + generic kind
(MCP, REST+key, OAuth, CLI) · D9 toolkits in C# · D10 resources per workflow/block/toolkit · D11 memory stores
· D12 "agents can make it a 99% headless experience".

## Scope
EARS criteria in tdd.html "Requirements (EARS)". Author's calls (not owner decisions): the 10 items in the issue's
"Settled before building" section.

## Pages
tdd.html (engineering + PR cards), tdd-share.html + deck.html (4 chapters, 33 slides), product-design.html.
Mockups are HTML sketches in product-design.html, not built from Sindri's Avalonia components (the canvas is PR17's job).
