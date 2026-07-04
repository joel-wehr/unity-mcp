# Unity MCP Server — Roadmap & Gap Analysis

_Owner: PM agent. Last updated: 2026-07-04._
_Derived from a 3-track research sweep (Unity's official AI/MCP, community repos, MCP protocol frontier). Sources at bottom._

---

## 1. Competitive landscape (mid-2026)

**Unity now ships a first-party MCP server** (`com.unity.ai.assistant`, Unity 6000+):
auto-starting in-Editor bridge + relay binary, native multi-instance targeting,
C#-attribute custom-tool authoring, a connection-approval security model, and support
for Claude Code / Cursor / Windsurf / Claude Desktop out of the box. **But it requires a
Unity subscription.**

The community leader is **CoplayDev/unity-mcp** (~11.5k★, was justinpbarnett; MIT,
Python/FastMCP server + C# plugin, 50–70 tools). Other serious repos: IvanMurzak/Unity-MCP
(reflection-based tools, runtime builds), CoderGamester/mcp-unity (**our architectural
ancestor** — WebSocket:8090 + Node/TS + C# plugin, batch+rollback), AnkleBreaker-Studio
(268 tools).

### Our differentiation story
1. **Free & open-source** — no Unity subscription required (Unity's official one needs it).
2. **Breadth** — 50 tools today; credible path to full-Editor coverage.
3. **XREAL One Pro mixed-reality support** — 32 XREAL tools nobody else has. This is our moat.
4. **Node/TS** — approachable contributor base vs. the Python leader.

We are **not** trying to beat Unity's official server on Editor-native integration; we win
on openness, breadth, and XR.

---

## 2. Where we stand

**Strengths (already have):** scene/GameObject/component CRUD, prefabs, asset mgmt, package
manager, play mode, menu items, console read/write, tests, recompile, execute-arbitrary-C#
(`execute_code`), build pipeline, profiler, physics/2D, materials/shaders, lighting, terrain,
navmesh, tilemap, sprites, particles, animation, scriptable objects, file ops, external DLLs,
debugger, undo/redo, editor control, watch-console (async), Unity Hub, asset store, + full
XREAL suite. **50 tool files · 17 resources · 13 prompts · 34 C# handlers.** Typecheck clean.

**Architecture:** `Claude ⇄ stdio ⇄ Node/TS MCP server ⇄ WebSocket:8090 ⇄ Unity C# plugin`.
This matches the community consensus split. Good foundation.

---

## 3. Gap analysis (verified against our code)

| Gap | Status in our repo | Leaders who have it |
|---|---|---|
| **Deprecated SDK API** | All ~82 tools use `server.tool()`; SDK installed is 1.25.1 (pin says ^1.7.0) | n/a — hygiene |
| **Tool annotations** (readOnly/destructive hints) | **0 tools annotated** | spec GA since 2025-03 |
| **Structured output** (`outputSchema`/`structuredContent`) | **0 tools**; all return `JSON.stringify` text | CoplayDev, spec GA |
| **Progress + cancellation** on slow ops | Not wired (build/test/import block silently) | spec GA |
| **Elicitation** for destructive confirms / missing params | Returns errors instead | spec GA since 2025-06 |
| **Resource subscriptions** (push vs poll) | Unverified — likely poll-only | spec GA |
| **Structured script editing** (create→apply-edits→validate + checksum) | Only raw file writes via `file_operations` | CoplayDev (distinctive) |
| **Set object/component *references*** | `update_component` = primitives only; must use `execute_code`+SerializedObject | all leaders |
| **Batch ops w/ rollback** | None | CoderGamester, CoplayDev |
| **Screenshot / isolated render** for visual verify | Editor screenshot exists in `editor_control`? (verify) | CoplayDev, IvanMurzak, HuntNight |
| **Live Unity docs + C# reflection lookup** | `search_unity_knowledge` (RAG) only | CoplayDev (distinctive) |
| **Tool-group toggling** (`manage_tools`) to cut token cost | None — all 50+ always advertised | CoplayDev |
| **Multi-Editor-instance targeting** | Single `UNITY_PORT`; one Unity at a time | Unity official, CoplayDev |
| **Custom tool authoring** without editing TS source | None — hardcoded | Unity official, IvanMurzak, all |
| **Connection approval / trust UX** | None | Unity official |
| **AI asset generation** wired to AssetDatabase | None | CoplayDev (we have Cockpit/Higgsfield though!) |
| **Build Profiles API** (Unity 6) | `build_pipeline` may use legacy BuildPipeline — verify | — |
| **Namespacing** (50+ flat tool names) | Flat `create_scene` etc. | best-practice at this scale |

---

## 4. Prioritized backlog

### P0 — Protocol modernization + correctness (do first; high leverage, low risk)
- [ ] **P0.1** Bump SDK pin `^1.7.0` → `^1.29.0`; migrate all `server.tool()` → `registerTool()`
      (do via a shared helper so we don't touch 82 files by hand — wrap in a `defineTool()`).
- [ ] **P0.2** Add **tool annotations** everywhere: `readOnlyHint:true` on all `get_*`/`find_*`/
      list/read tools; `destructiveHint:true` on `delete_scene`, `delete_gameobject`,
      `delete_asset`, `manage_asset` delete paths, `execute_code`, `file_operations` writes.
- [ ] **P0.3** Add **`outputSchema` + `structuredContent`** to structured-data tools
      (`get_gameobject`, `find_gameobjects`, `get_console_logs`, `project_settings`, profiler,
      build status, editor state) while keeping the text block for back-comat.
- [ ] **P0.4** **Progress notifications + cancellation** for `run_tests`, `recompile_scripts`,
      `build_pipeline`, `asset_import`, lighting bake. (We already have async job pattern in
      `watch_console`/build status — extend it.)
- [ ] **P0.5** **Error hygiene**: return tool-execution errors (`isError:true`) for
      self-correctable failures instead of throwing protocol errors. Audit `errors.ts` + handlers.

### P1 — High-value capability parity (close the biggest feature gaps)
- [ ] **P1.1** **Set object/component references** in `update_component` (the #1 known limitation) —
      resolve target by path/id and set via SerializedObject on the C# side. Removes the most
      common reason to fall back to `execute_code`.
- [ ] **P1.2** **Structured script editing tool**: `create_script`, `apply_script_edits`
      (line/anchor-based), `validate_script` (compile check), with `get_sha` checksums to detect
      stale edits. Biggest workflow win from CoplayDev.
- [ ] **P1.3** **Batch execution with rollback**: wrap N ops in one Undo group; revert all on
      failure. Pairs well with our existing undo/redo handler.
- [ ] **P1.4** **Screenshot / isolated-object render** tool returning an image (register results
      in Cockpit Gallery) so the agent can visually verify scene changes.
- [ ] **P1.5** **Live docs + reflection**: `unity_reflect` (inspect real C# API surface of a type/
      method) and `unity_docs` (fetch docs.unity3d.com) so the agent stops hallucinating APIs.
- [ ] **P1.6** **Elicitation** for destructive confirms + missing required params.

### P2 — Scale & differentiation
- [ ] **P2.1** **Tool-group toggling** (`manage_tools`) + **namespacing** audit — as we pass 60+
      tools, advertise only active groups to cut tool-list token cost and improve selection.
- [ ] **P2.2** **Multi-Editor-instance** support: connect to multiple Unity editors, `set_active_instance`.
- [ ] **P2.3** **C#-side custom tool authoring** (attribute-based) so users add tools without editing TS.
- [ ] **P2.4** **AI asset generation** tool — leverage the Cockpit/Higgsfield MCP already in this
      workspace to generate textures/3D and import straight into AssetDatabase. Unique combo.
- [ ] **P2.5** **DSL / spatial scene queries** (grep-like over scene graph + radius/proximity).

### P3 — Watch / anticipate (don't build yet)
- Unity **Build Profiles**, **Verified/signed Packages**, **Platform Toolkit** (6.3+) — verify our
  build/package tools against the newer APIs; wrap when we touch them.
- **CoreCLR migration** (2026 rolling) — audit `execute_code`/`debugger` reflection for compat.
- **MCP 2026-07-28 spec** (RC, finalizes ~Jul 28) — Tasks extension is relevant to our slow ops;
  MCP Apps could power a dashboard. **Do NOT build against the RC or SDK v2 beta yet** (v2 is
  ESM-only/Node20+, breaking). Revisit after GA + client ecosystem catches up.
- **Netcode unification / ECS-as-core (6.4)** — future multiplayer/Entities tool category.
- Sampling/Roots/Logging being **deprecated** in the RC — don't invest in Sampling-based features.

---

## 5. Known bugs / debt
- `update_component` can't set references (→ P1.1).
- SDK pin (`^1.7.0`) lags installed (1.25.1) and latest (1.29.0) (→ P0.1).
- `.claude/CLAUDE.md` tracker counts were stale (fixed alongside this roadmap).
- No tests / CI; no live Unity project to validate tools end-to-end (see open question).
- `execute_code` / newline / duplicate-type issues were fixed (see MCP_GAPS_LOG.md) — keep regression notes.

## 6. Open questions for the user
- **Testing infra**: stand up a Unity project in `C:\Users\joelw\joelwehr.com\Unity` + CI to
  validate tools live? Recommended before large P1 changes.
- **Workflow**: self-implement roadmap items straight to `main`, or open PRs for review?
- **Positioning**: is "free/open + breadth + XREAL" the intended wedge vs. Unity's official server?

---

## Sources
Unity official MCP (`com.unity.ai.assistant` docs 2.7/2.10, unity.com/blog MCP posts),
CoplayDev/unity-mcp, IvanMurzak/Unity-MCP, CoderGamester/mcp-unity, AnkleBreaker-Studio/unity-mcp-server,
HuntNight/unity-mcp-advanced; MCP spec 2025-11-25 + 2026-07-28 RC (blog.modelcontextprotocol.io),
@modelcontextprotocol/sdk (npm 1.29.0 / v2 beta), Anthropic "Writing effective tools for AI agents".
