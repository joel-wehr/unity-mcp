# Unity MCP — PM Memory

_Last updated: 2026-07-04 (after P0b–d: structured output, progress+cancel, error hygiene, SDK 1.29)_

## Quick resume (start here)
Repo is CLEAN, on `main`, all pushed (commit: P0b-d). **All of P0 is now DONE.**
`tsc`+build clean; server boots and registers **84 tools** over a real STDIO handshake.
SDK bumped to **1.29.0**. **Next up: P1** — recommend **P1.1 update_component
object-reference fix** (the #1 known bug: today can't wire object/component refs,
needs execute_code + SerializedObject). Other P1: structured script editing,
batch+rollback, screenshot verify, live docs/reflection, elicitation. See ROADMAP.md.
Test workflow + commands are in launch.md.

## ⚠️ One open verification (do when Unity is next up)
P0b structuredContent was validated against the SDK (schemas advertised, boot OK) but
NOT yet against a LIVE Unity response (Unity wasn't running). Schemas are deliberately
permissive (all fields optional, `.passthrough()`, instanceId accepts number|string) so
risk is low, but do a live round-trip on the testbed to confirm get_gameobject /
find_gameobjects / get_console_logs / run_tests don't trip output validation → isError.

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

## Decisions (user, 2026-07-04)
- **Workflow:** direct-to-main + notify (no PRs). Authorized to push to origin/main.
- **Test infra:** yes — stand up a live Unity project + CI. DONE (see below).
- **First work:** P0 protocol modernization. P0a done; P0b–d remain.

## Progress log (continued)
- 2026-07-04: Added **CI** (`.github/workflows/ci.yml`, typecheck+build on Node 20/22)
  and `.gitattributes` (LF normalization). Pushed.
- 2026-07-04: **P0a DONE** — migrated all ~82 tools `server.tool()` → `registerTool()`
  + centralized MCP tool annotations (`src/utils/toolAnnotations.ts`). Verified: boots,
  all tools register, tsc+build clean. Committed 9179ae0.
- 2026-07-04: **Test infra DONE** — created Unity 6 testbed at
  `C:\Users\joelw\joelwehr.com\Unity\McpTestbed` (6000.5.2f1), embedded plugin,
  validated full round-trip (get_console_logs/send_console_log/create_scene) on 6.5.
- 2026-07-04: **Fixed 2 install-blocking Unity 6/6.5 bugs** (found via live compile,
  committed 657a0a8): (1) missing `com.unity.ugui` dependency; (2) Unity 6.5
  InstanceID→EntityId obsolete-errors → added `McpId` reflection shim + codemodded
  64 sites. See MCP_GAPS_LOG.md 2026-07-04 entry.

## P0 — DONE (all four, 2026-07-04)
- ✅ P0a: registerTool migration + annotations (earlier commit 9179ae0).
- ✅ P0b: outputSchema + structuredContent on **get_gameobject, find_gameobjects,
  get_console_logs, run_tests**. Permissive schemas (optional/passthrough, id fields
  number|string). Deferred project_settings/profiler — highly variable shapes, low ROI;
  add later if a client needs typed output from them.
- ✅ P0c: `src/utils/progress.ts` (sendUnityRequestWithProgress) — time-based progress
  heartbeats (only when client sends progressToken) + AbortSignal forwarding. New
  `ErrorType.CANCELLED`. `McpUnity.sendRequest` takes an AbortSignal (stripped before
  wire). Wired into run_tests, build_pipeline, recompile_scripts, asset_import, lighting.
  NOTE: Node-side cancel only stops the wait — Unity keeps running the op (plugin has no
  cancel channel; future work is a Unity-side cancel message).
- ✅ P0d: VERIFIED via STDIO test — thrown handler errors (Unity down / Unity failure)
  AND input-validation errors both surface as `isError: true` results, no protocol
  errors leak. SDK 1.29 wraps even InvalidParams as isError.
- ✅ SDK pin ^1.7.0 → ^1.29.0 (installed 1.29.0; had to `npm install --include=dev` to
  restore typescript — devDeps were missing from node_modules).

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
   component references (must use `execute_code` + SerializedObject). = **P1.1**, next up.
3. P0b structuredContent not yet validated against a LIVE Unity response (see warning at
   top). Low risk (permissive schemas). Do on next testbed session.
4. UPM manifest had a dangling `samples[]` → `Samples~/XrealDemo` (folder never existed);
   **removed** 2026-07-04. If we want a real in-package demo, build a `Samples~/XrealDemo`
   scene (hand tracking + spatial anchors) and re-add the samples entry. Backlog, not urgent.

## Next steps
- [ ] **P1.1** — update_component: set object/component references (top pick).
- [ ] Live-validate P0b structuredContent on the McpTestbed when Unity is next open.
- [ ] Refresh `.claude/CLAUDE.md` tracker counts (says 22+15; actual 50 tool files).
- [ ] (backlog) Build a real `Samples~/XrealDemo` scene, then re-add samples[] to UPM manifest.
