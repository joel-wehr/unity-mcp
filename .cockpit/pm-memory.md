# Unity MCP — PM Memory

_Last updated: 2026-07-04 (PM given standing mandate)_

## Standing mandate (from user, 2026-07-04)
I am the PM that **controls this repo**. Goals:
- Build a **comprehensive, bleeding-edge** Unity MCP server.
- Continuously review what **Unity ships itself** + leading **public repos**; close gaps.
- Triage **GitHub issues** submitted by other agents (features + bugs) — repo:
  `joel-wehr/unity-mcp` (via `gh`, authed as joel-wehr). None open as of 2026-07-04.
- Keep the repo **clean, committed, orderly**. (Authorized to commit + push to main.)
- Spawn **subagents** (appropriate models) for research and tasks.
- Unity **test project** goes in `C:\Users\joelw\joelwehr.com\Unity` (exists, empty).

## Progress log
- 2026-07-04: Committed the entire outstanding body of work in 3 logical commits
  (Unity C# plugin / MCP server expansion / docs+memory) and **pushed to origin/main**.
  Removed the `nul` junk file. Repo is now CLEAN. Radar risk item cleared.
- 2026-07-04: 3 research agents (Sonnet) completed. Wrote **ROADMAP.md** (gap analysis
  + prioritized P0–P3 backlog) and committed it. Updated `.claude/CLAUDE.md` changelog.

## Key research findings (2026-07-04) — see ROADMAP.md for detail
- **Unity ships its own first-party MCP now** (`com.unity.ai.assistant`, Unity 6000+,
  auto-start bridge + relay, custom-tool SDK, connection approval) — **but subscription-gated.**
- **Community leader:** CoplayDev/unity-mcp (~11.5k★, was justinpbarnett; Python/FastMCP).
  Our repo descends from CoderGamester/mcp-unity (WebSocket:8090 + Node/TS + C# plugin).
- **Our wedge:** free/open-source + tool breadth + **XREAL One Pro** (our moat, 32 XR tools).
- **Verified code facts:** all ~82 tools use deprecated `server.tool()`; SDK installed is
  **1.25.1** (pin says ^1.7.0, latest 1.29.0) so `registerTool()` is available now; ZERO
  tools have annotations/outputSchema/structuredContent.
- **P0 backlog** (highest leverage): registerTool migration + tool annotations (readOnly/
  destructive) + structured output + progress/cancellation on slow ops + error hygiene.
- **P1:** set object refs in update_component (#1 known bug), structured script editing,
  batch+rollback, screenshot verify, live docs/reflection, elicitation.
- **Do NOT** adopt MCP SDK v2 beta / 2026-07-28 RC yet (ESM-only, breaking; not GA).

## What this project is
A unified **Model Context Protocol server** that lets an AI assistant drive the
Unity Editor. Two halves:
- **Node/TypeScript MCP server** (`src/`) — exposes tools/resources/prompts over STDIO.
- **Unity C# editor plugin** (`UnityPlugin/`) — runs inside Unity, hosts a WebSocket
  server the Node side connects to.

Architecture: `Claude Code <-> STDIO <-> Node MCP server <-> WebSocket (port 8090) <-> Unity Editor (C#)`

Strong focus on **XREAL One Pro mixed-reality** development (Samsung S24 + AR glasses),
on top of general Unity editor control.

## Current surface area (as of this run)
- `src/tools/` — **50** tool files
- `src/resources/` — 11 core + 6 XREAL = **17** resources
- `src/prompts/` — 9 core + 4 XREAL = **13** prompts
- `UnityPlugin/Editor/Handlers/` — **34** C# handlers

`.claude/CLAUDE.md` is an "AI development tracker" — it lists implemented tools and a
changelog, and is meant to be edited (Features/Issues sections) to trigger dev work.
Note: its counts are now stale (says 22+15+... tools; actual is 50 tool files).

## Status
- ✅ `npx tsc --noEmit` is **clean** — the TypeScript compiles.
- ✅ `build/` directory exists (compiled output present, gitignored).
- ✅ **Repo is clean and pushed** to origin/main (as of 2026-07-04). `nul` junk removed.

## Recent development context (from MCP_GAPS_LOG.md)
Work has been driven by actually building an iOS/XREAL game and logging every gap.
Last logged session (2026-05-20, "QR scanning pipeline") fixed 7 items end-to-end:
- Unity 6 vs 2022.3 API compat in Physics/Physics2D/NavMesh handlers (`#if UNITY_6000_0_OR_NEWER` guards)
- `execute_code` duplicate-type / newline-escaping bugs
- `build_pipeline set_scenes` implemented
- async `build_xreal_apk` + new `get_build_status` (10s MCP timeout was killing long builds)
- new `add_external_dll` tool
- centralized XREAL SDK detection
- real AR Foundation camera capture in `get_camera_frame`

**Known still-open gap:** `update_component` only sets primitive fields — can't wire
up object/component references (has to be done via `execute_code` + SerializedObject).

## Open threads / risks
1. `.claude/CLAUDE.md` implementation counts are stale vs. actual file counts
   (says 22+15; actual = 50 tool files). Refresh when doing roadmap.
2. Known code gap: `update_component` only sets primitive fields — can't wire object/
   component references (must use `execute_code` + SerializedObject). Roadmap candidate.
3. No automated tests / CI yet. No Unity test project set up to validate tools live.

## Next steps
- [ ] (in flight) Collect 3 research agents' findings → write ROADMAP.md (task #3).
- [ ] Refresh `.claude/CLAUDE.md` tracker counts.
- [ ] Decide whether to set up a Unity test project + CI to validate tools.
- [ ] Consider MCP SDK upgrade (currently ^1.7.0) once protocol research lands.
