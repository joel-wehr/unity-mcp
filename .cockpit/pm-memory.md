# Unity MCP — PM Memory

_Last updated: 2026-07-04 (first PM run)_

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
- ✅ `build/` directory exists (compiled output present).
- ⚠️ **Large uncommitted body of work.** 79 changed/untracked items. The ENTIRE
  `UnityPlugin/` C# folder, ~30 new tools, new resources/prompts, and several docs
  (`MCP_GAPS_LOG.md`, `SETUP_GUIDE.md`, `.mcp.json`) are untracked. Git history is
  only 2 commits (initial + tracker). **This work is unversioned and at risk.**
- 🧹 A junk file named `nul` (126 KB JSON, a Windows `> nul` redirect artifact) is
  sitting in the repo root — should be deleted.

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
1. **Commit the work.** Nothing is versioned. Recommend committing (or at least a WIP
   commit) so months of plugin + tool work isn't lost. Needs user go-ahead.
2. **Delete `nul`** junk file.
3. `.claude/CLAUDE.md` implementation counts are stale vs. actual file counts.

## Next steps (proposed, awaiting user)
- [ ] Confirm whether to commit the outstanding work, and on what branch.
- [ ] Remove `nul` artifact.
- [ ] Optionally refresh the tracker counts in `.claude/CLAUDE.md`.
