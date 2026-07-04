# Unity MCP Server - AI Development Tracker

This file is monitored by Claude Code for event-driven development. When changes are made to the Features or Issues sections below, the AI will review Unity and Anthropic MCP documentation to implement solutions.

**Last Reviewed:** 2026-07-04

---

## My Role (Monitoring AI Agent)

I am a senior developer AI agent responsible for maintaining and evolving this Unity MCP server. My job is:

### What I Do
1. **Monitor this file** - I watch for changes to the Features and Issues sections
2. **Research documentation** - When updates are detected, I review:
   - Unity Editor scripting documentation (UnityEditor, UnityEngine APIs)
   - Unity Package Manager documentation
   - Anthropic MCP SDK documentation and best practices
   - Existing codebase patterns
3. **Implement solutions** - I write TypeScript code for the MCP server and coordinate with Unity C# requirements
4. **Commit changes** - All work is committed to git with descriptive messages
5. **Update this file** - I mark features as completed and issues as resolved

### How to Request Work
**For new features:** Add an entry under `## Features` with:
- Feature name, status, priority, description
- Relevant Unity APIs to research
- Any additional context

**For bugs/issues:** Add an entry under `## Issues` with:
- Issue title, status, severity
- Steps to reproduce, expected vs actual behavior
- Error messages if applicable

### Event-Driven Workflow
```
Another AI adds Feature/Issue to this file
         ↓
I detect the change
         ↓
I research Unity + MCP documentation
         ↓
I implement the solution in src/
         ↓
I commit to git
         ↓
I update this file (status → Completed/Resolved)
```

---

## Features

<!--
Add feature requests here. The monitoring AI will:
1. Review Unity documentation for relevant APIs
2. Review Anthropic MCP documentation for best practices
3. Implement the feature following MCP patterns
4. Update this file when complete

Format:
### [Feature Name]
- **Status**: Pending | In Progress | Completed
- **Priority**: High | Medium | Low
- **Description**: Brief description of the feature
- **Unity APIs**: Relevant Unity APIs to research
- **Notes**: Additional context
-->


---

## Issues

<!--
Report bugs and issues here. The monitoring AI will:
1. Investigate the root cause
2. Review relevant Unity/MCP documentation
3. Implement a fix
4. Update this file when resolved

Format:
### [Issue Title]
- **Status**: Open | Investigating | Resolved
- **Severity**: Critical | Major | Minor
- **Description**: What's happening
- **Steps to Reproduce**: How to trigger the issue
- **Expected Behavior**: What should happen
- **Actual Behavior**: What actually happens
- **Error Messages**: Any relevant error output
-->


---

## Changelog

<!-- Automatically updated when features are completed or issues are resolved -->

### 2026-07-04 (P0b–d: protocol modernization pt.2)
- **P0b — structured output:** added `outputSchema` + `structuredContent` to the core
  structured readers `get_gameobject`, `find_gameobjects`, `get_console_logs`, `run_tests`.
  Schemas are permissive (fields optional, `.passthrough()`, id fields accept number|string
  for post-6.5 EntityId compat) so a valid Unity response never fails output validation.
- **P0c — progress + cancellation:** new `src/utils/progress.ts`
  (`sendUnityRequestWithProgress`) emits time-based `notifications/progress` heartbeats
  (only when the client sends a `progressToken`) and forwards the client's `AbortSignal`.
  `McpUnity.sendRequest` now accepts an `AbortSignal`: an aborted call stops waiting and
  rejects with `ErrorType.CANCELLED` (Unity-side cancel still needs plugin support — roadmap).
  Wired into `run_tests`, `build_pipeline`, `recompile_scripts`, `asset_import`, `lighting`.
- **P0d — error hygiene:** verified end-to-end that thrown handler errors (Unity down,
  Unity-reported failures) AND input-validation errors surface as well-formed
  `isError: true` tool results — no JSON-RPC protocol errors leak from `tools/call`.
- Bumped `@modelcontextprotocol/sdk` pin `^1.7.0` → `^1.29.0` (installed 1.29.0).
- Verified: `tsc --noEmit` clean, `npm run build` clean, server boots and registers 84 tools
  over a real STDIO handshake with the new schemas/annotations advertised.

### 2026-07-04
- Committed the full outstanding body of work (Unity C# plugin + expanded TS server + docs) and pushed to origin/main; repo is clean.
- Ran a 3-track competitive/protocol research sweep and wrote **ROADMAP.md** (gap analysis + prioritized backlog).
- Key finding: Unity now ships an official first-party MCP server (`com.unity.ai.assistant`, subscription-gated); community leader is CoplayDev/unity-mcp (~11.5k★). Our wedge: free/open + breadth + XREAL.
- Top gaps identified: deprecated `server.tool()` API (no annotations/output schemas), no progress/cancellation on slow ops, `update_component` can't set object references, no structured script-editing or batch+rollback. See ROADMAP.md.

### 2026-01-15
- Added 15 comprehensive control tools for complete Unity control
- Added project_settings tool (PlayerSettings, QualitySettings, GraphicsSettings, etc.)
- Added script_management tool (define symbols, assembly definitions, execution order)
- Added profiler tool (frame capture, memory snapshots, performance counters)
- Added build_pipeline tool (build, settings, scenes, reports, platform switching)
- Added editor_control tool (window management, inspector, scene view)
- Added undo_redo tool (undo/redo history, grouping)
- Added watch_console tool (recursive iteration, wait for messages/errors/compilation)
- Added debugger tool (expression evaluation, object dumps, method invocation)
- Added asset_import tool (texture, model, audio import settings)
- Added animation tool (Animator control, Timeline, recording)
- Added physics tool (raycasts, simulation, forces)
- Added material_shader tool (materials, shaders, keywords)
- Added lighting tool (baking, ambient, fog, skybox)
- Added unity_hub tool (create projects, manage editors - works without Unity running)
- Added asset_store tool (download and import purchased assets)
- Added 4 new resources (profiler, build status, editor state, project settings)

### 2026-01-12
- Added comprehensive XREAL One Pro mixed reality support (32 new tools)
- Added XREAL resources (6 new resources)
- Added XREAL workflow prompts (4 new prompts)
- Added XREAL-specific error types

### 2025-12-21
- Initial Unity MCP Server with 22 tools, 7 resources, 1 prompt
- Created AI development tracker

---

## Current Implementation Status

### Core Unity Tools (22)
- [x] create_scene - Create new scenes
- [x] delete_scene - Delete scenes
- [x] load_scene - Load scenes
- [x] get_gameobject - Get GameObject details
- [x] update_gameobject - Create/update GameObjects
- [x] delete_gameobject - Delete GameObjects
- [x] duplicate_gameobject - Clone GameObjects
- [x] find_gameobjects - Search GameObjects
- [x] select_gameobject - Select in editor
- [x] update_component - Modify components
- [x] editor_selection - Get/set selection
- [x] create_prefab - Create prefabs
- [x] add_asset_to_scene - Instantiate assets
- [x] manage_asset - Asset operations
- [x] add_package - Package Manager integration
- [x] play_mode - Control play mode
- [x] execute_menu_item - Run menu items
- [x] recompile_scripts - Force recompilation
- [x] get_console_logs - Read console
- [x] send_console_log - Write to console
- [x] run_tests - Execute tests
- [x] search_unity_knowledge - RAG documentation search

### Comprehensive Control Tools (15)
- [x] project_settings - Get/set PlayerSettings, QualitySettings, GraphicsSettings, PhysicsSettings, etc.
- [x] script_management - Define symbols, assembly definitions, script execution order
- [x] profiler - Frame capture, memory snapshots, render stats, GC allocations, CPU/GPU usage
- [x] build_pipeline - Build, settings, scenes, reports, platform switching, player settings
- [x] editor_control - Window management, inspector lock/debug, scene view, refresh, screenshots
- [x] undo_redo - Undo/redo history, grouping, object recording
- [x] watch_console - Wait for messages/errors/compilation, recursive iteration support
- [x] debugger - Expression evaluation, object dumps, method invocation, static field access
- [x] asset_import - Texture, model, audio import settings and reimport
- [x] animation - Animator control, Timeline playback, animation recording
- [x] physics - Raycasts, sphere/box casts, overlaps, forces, layer collision matrix
- [x] material_shader - Material properties, shader assignment, keywords, global properties
- [x] lighting - Lightmap baking, ambient, fog, skybox, reflection probes
- [x] unity_hub - Create projects, open projects, list/install editors, add modules (works without Unity running)
- [x] asset_store - List/search purchased assets, download and import from Asset Store (requires Unity login)

### XREAL Project Setup Tools (4)
- [x] setup_xreal_project - Configure Unity project for XREAL One Pro
- [x] configure_android_build - Android build settings for Samsung S24
- [x] import_nrsdk - Import NRSDK package
- [x] validate_xreal_setup - Validate project configuration

### XREAL Device Tools (4)
- [x] get_xreal_device_info - Query connected device status
- [x] set_tracking_mode - Switch 0DoF/3DoF/6DoF tracking
- [x] calibrate_glasses - IPD and display calibration
- [x] get_camera_frame - Capture RGB camera frame

### XREAL Hand Tracking Tools (4)
- [x] enable_hand_tracking - Enable/configure hand tracking
- [x] get_hand_state - Query hand pose and gestures
- [x] configure_hand_gestures - Set up gesture recognition
- [x] create_hand_interactable - Add hand interaction to GameObjects

### XREAL Spatial Mapping Tools (5)
- [x] enable_plane_detection - Detect floors, walls, tables
- [x] get_detected_planes - Query detected planes
- [x] create_spatial_anchor - Create persistent anchors
- [x] manage_spatial_anchors - Load/save/delete anchors
- [x] enable_meshing - Generate spatial mesh

### XREAL Image Tracking Tools (3)
- [x] add_tracking_image - Register images for detection
- [x] configure_image_tracking - Set tracking quality/count
- [x] get_tracked_images - Query detected images

### XREAL Mixed Reality Tools (3)
- [x] configure_passthrough - Camera passthrough settings
- [x] set_render_mode - AR/VR/MR rendering mode
- [x] configure_occlusion - Depth-based occlusion

### XREAL Build Tools (2)
- [x] build_xreal_apk - Build APK for XREAL
- [x] get_connected_devices - List ADB devices

### XREAL XR Interaction Tools (4)
- [x] setup_xr_interaction - Configure XR Interaction Toolkit
- [x] create_xr_rig - Create XREAL camera rig
- [x] add_xr_interactor - Add ray/direct interactors
- [x] create_xr_ui - World-space UI for XR

### XREAL Performance Tools (3)
- [x] get_xr_performance_metrics - Frame rate, GPU, thermals
- [x] profile_xr_scene - Analyze scene performance
- [x] capture_xr_screenshot - Capture XR viewport

### Core Unity Resources (7)
- [x] unity://scenes_hierarchy - All GameObjects in loaded scenes
- [x] unity://gameobject/{id} - Detailed GameObject info
- [x] unity://menu-items - Available menu items
- [x] unity://logs/{type} - Console log entries
- [x] unity://packages - Package Manager data
- [x] unity://assets - Asset Database contents
- [x] unity://tests/{mode} - Test Runner info

### Comprehensive Control Resources (4)
- [x] unity://profiler/{dataType} - Real-time profiler data (frame_timing, memory, rendering, cpu, gc_allocs, physics)
- [x] unity://build/{infoType} - Build pipeline info (settings, scenes, report, platforms)
- [x] unity://editor_state - Current editor state (play mode, compilation, selection, etc.)
- [x] unity://settings/{category} - Project settings by category (player, quality, graphics, physics, etc.)

### XREAL Resources (6)
- [x] xreal://device_state - Device connection and tracking status
- [x] xreal://hand_tracking/{hand} - Hand joint positions and gestures
- [x] xreal://spatial_anchors - All spatial anchors in scene
- [x] xreal://detected_planes - Detected environment planes
- [x] xreal://tracked_images - Currently tracked images
- [x] xreal://build_settings - Android/XREAL build configuration

### Core Unity Prompts (1)
- [x] gameobject_handling_strategy - GameObject workflow guidance

### XREAL Prompts (4)
- [x] xreal_project_setup - Step-by-step project setup guide
- [x] hand_interaction_strategy - Hand tracking best practices
- [x] spatial_anchor_workflow - Persistent MR experiences
- [x] xreal_optimization_guide - Mobile XR performance optimization

---

## Architecture Reference

```
Claude Code <-> STDIO <-> Node.js MCP Server <-> WebSocket <-> Unity Editor (C#)
```

**Key Files:**
- `src/index.ts` - Server entry point, registers all tools/resources
- `src/unity/mcpUnity.ts` - Unity WebSocket bridge (JSON-RPC)
- `src/tools/*.ts` - MCP tool implementations
- `src/resources/*.ts` - MCP resource implementations
- `src/utils/` - Logger and error handling

**Adding New Tools:**
1. Create `src/tools/myNewTool.ts`
2. Define Zod schema for parameters
3. Register in `src/index.ts`
4. Implement Unity C# handler

**Adding New Resources:**
1. Create `src/resources/myNewResource.ts`
2. Define URI pattern
3. Register in `src/index.ts`
4. Implement Unity data provider

---

## Notes
- Issues are reported by AI agents using the MCP server in Unity projects
- This file is monitored for changes - add Features/Issues above to trigger development
- Priority: Bugs that block workflows > New features > Enhancements
