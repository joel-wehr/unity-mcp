# MCP Server Gaps & Issues Log

This document tracks problems encountered while building an iOS game, and features that need to be added to the MCP server.

## Session: 2026-05-20 (XREAL-DEV — QR scanning pipeline)

### Errors & Bugs Fixed
- [x] PhysicsHandler.cs uses Unity 6 names (`linearVelocity`, `linearDamping`, `angularDamping`) on a Unity 2022.3 project | Plugin failed to compile, bridge never bound port 8090 | Fixed: wrapped all three properties in `#if UNITY_6000_0_OR_NEWER` / `#else` blocks falling back to `velocity` / `drag` / `angularDrag`
- [x] Physics2DHandler.cs uses Unity 6 `Rigidbody2D.linearDamping` / `angularDamping` on 2022.3 | Same plugin-failed-to-compile root cause | Fixed: same `#if` pattern with fallback to `drag` / `angularDrag`
- [x] NavMeshHandler.cs called `NavMesh.GetAreaNames()` which doesn't exist on `UnityEngine.AI.NavMesh` in 2022.3 (and `UnityEditor.AI.NavMesh` is not a type) | Compile error CS0117/CS0234 | Fixed: switched to `UnityEditor.GameObjectUtility.GetNavMeshAreaNames()` (the actual public 2022.3 API; still present in Unity 6)

### Issues Encountered (Open)
- [x] `execute_code` rejects any C# source that references `System.Type`, `System.IO.File`, `System.Text.StringBuilder`, `System.Collections.Generic.List` (even fully-qualified or `global::`-qualified) | Status: Fixed | Tightened `CodeExecutionHandler.ExecuteWithCodeProvider`: skip reference-only assemblies (marked with `ReferenceAssemblyAttribute`), drop the `netstandard` facade when `mscorlib` is present, and dedupe by simple assembly name. Smoke-test snippets documented in the source.
- [x] Newline literal `"\n"` inside a string in `execute_code` payloads is interpreted by the JSON-RPC params parser as a real newline before reaching csc, producing CS1010 "Newline in constant" | Status: Fixed | Replaced the chained `.Replace("\\n","\n")` calls in `JsonRpcParamsParser.ParseToDictionary` with a single-pass JSON-string unescape that consumes each escape exactly once. `\\n` on the wire now correctly yields the two characters `\` + `n`; `\n` on the wire still yields a real newline.
- [x] `build_pipeline` action=`set_scenes` returns `Build pipeline action 'set_scenes' failed` with no detail | Status: Fixed | `BuildHandler.SetScenes` now parses a `scenes` array (`[{path, enabled}, ...]`) and assigns `EditorBuildSettings.scenes`. Includes path normalization (absolute -> Assets-relative) and an entry-skip log for malformed items.
- [x] `build_xreal_apk` blocks on a synchronous request that takes minutes; MCP client times out at 10s while the build keeps running on the Unity side. | Status: Fixed | `BuildXrealApk` now allocates a jobId, stashes a `BuildJob` in a static dict, queues `RunBuildJob` on `EditorApplication.delayCall`, and returns immediately with the jobId. New tool `get_build_status` exposes the job state (Queued/Building/Succeeded/Failed) plus outputPath, fileSizeMb, buildSeconds, error list, and (when `runAfterBuild=true`) the install/launch result.

### Already-known gaps confirmed this session
- Set component references via MCP — confirmed still missing. All scene wiring (ARCameraManager onto EyeCameraSource, etc.) had to go through `execute_code` + `SerializedObject.FindProperty(...).objectReferenceValue`. The `update_component` tool only handles primitive fields. **Still open.**
- [x] `get_camera_frame` returned a grey placeholder. Status: Fixed | `XrealDeviceHandler.GetCameraFrame` now attempts an AR Foundation `ARCameraManager.TryAcquireLatestCpuImage()` -> `XRCpuImage.Convert(...)` capture via reflection (so the plugin doesn't take a compile-time dep on AR Foundation). Falls back to the labelled grey placeholder when not in play mode, when no ARCameraManager is in the scene, or when AR Foundation isn't present. Note explains which case was hit.
- [x] `validate_xreal_setup` / `IsNrsdkAvailable` detected only legacy NRSDK. Status: Fixed | Centralized in `XrealSdkDetector` (in `XrealDeviceHandler.cs`). Recognizes any of: `Assets/NRSDK/`, `NRKernal.*` types in any loaded assembly, the `com.xreal.xr` UPM package (resolved folder OR manifest entry), or any `Unity.XR.XREAL.*` runtime type. `validate_xreal_setup` updated to use the new detector and surface a clearer "no XREAL SDK" diagnostic.
- [x] No "install NuGet / external DLL" tool. Status: Fixed | New `add_external_dll` tool. Node side downloads the URL (axios, 250MB cap) to a temp file and asks the Unity Editor's `AssetHandler.AddExternalDll` to copy it into `Assets/<destinationFolder>/<fileName>` and refresh the AssetDatabase. Path-traversal blocked on both ends.

### Session summary (this work)
All seven work items completed end-to-end:
1. Fixed execute_code duplicate-type errors (CodeExecutionHandler.cs).
2. Fixed JsonRpcParamsParser newline handling (McpUnityBridge.cs).
3. Implemented set_scenes (BuildHandler.cs).
4. Async build_xreal_apk + new get_build_status tool (XrealBuildHandler.cs + buildXrealApkTool.ts + getBuildStatusTool.ts).
5. Added add_external_dll tool (addExternalDllTool.ts + AssetHandler.cs).
6. Centralized XREAL SDK detection (XrealSdkDetector in XrealDeviceHandler.cs; XrealProjectHandler.cs).
7. Real AR Foundation camera capture in get_camera_frame, with labelled fallback (XrealDeviceHandler.cs).

Key decisions:
- For (1), removed both the duplicate netstandard reference AND any assembly marked with `ReferenceAssemblyAttribute`, instead of just one. The combination is what eliminates CS0433 across the full set of system types reported.
- For (4), `BuildJob` storage is in-memory only (per editor domain). A domain reload during a build would lose state — acceptable trade-off vs. the complexity of session-persisted job state.
- For (5), the Node side downloads to a temp file and passes the path to Unity, rather than base64-shuttling the bytes over the JSON-RPC socket. Keeps multi-megabyte DLLs out of the message queue.
- For (7), AR Foundation reflection is used throughout — no compile-time dependency on `Unity.XR.ARFoundation` or the XREAL SDK packages, so the plugin still builds in projects without those packages. `npm run build` clean after each item.

---

## Session: 2026-01-15

### Issues Encountered
<!-- Format:
- [ ] Issue description | Status: Open/Fixed | Solution/Notes
-->
- [x] Unity Hub CLI doesn't support `create` command | Status: Fixed | Solution: Use Unity Editor directly with `-createProject` flag

### Missing MCP Features
<!-- Format:
- [ ] Feature needed | Priority: High/Medium/Low | Implemented: Yes/No
-->
- [ ] Force Unity recompilation from MCP | Priority: High | Implemented: Partial (recompile_scripts exists but timeouts when Unity is in background)
- [ ] Restart Unity programmatically | Priority: Medium | Implemented: No (needed when Unity doesn't detect file changes)
- [x] Build Pipeline Unity Handler | Priority: High | Implemented: Yes (created BuildHandler.cs)
- [ ] Project Settings Unity Handler | Priority: Medium | Implemented: No (TypeScript tool exists, Unity handler missing)
- [ ] Set component references via MCP | Priority: High | Implemented: No (cannot set object references, only primitive values)
- [ ] Set RectTransform anchors/sizeDelta via MCP | Priority: Medium | Implemented: No (workaround: Editor scripts with menu items)
- [ ] Get detailed component data including references | Priority: Medium | Implemented: Partial (returns types but not reference values)

### Errors & Bugs
<!-- Format:
- [ ] Error description | Root cause | Fix applied
-->
- [x] XrealXrInteractionHandler.cs - UnityEngine.UI namespace not found | asmdef reference was wrong | Fixed: Changed to "UnityEngine.UI" in asmdef
- [x] CoreResources.cs - PackageInfo.status doesn't exist | Unity 6 API change | Fixed: Changed to package.resolvedPath
- [x] MCP Server not auto-starting | AutoStart default was false | Fixed: Changed default to true and made server always start on load
- [x] MCP Server not restarting after domain reload | EditorApplication.delayCall not firing | Fixed: Use [InitializeOnLoadMethod] with EditorApplication.update callback
- [x] All MCP tools failing (create_scene, update_gameobject, execute_menu_item) | JsonRpcParams only had `uri` field, JsonUtility ignored other params | Fixed: Created JsonRpcParamsParser to extract raw params JSON and parse manually
- [x] execute_menu_item now works | Was parameter parsing issue | Fixed with JsonRpcParamsParser

### Unity Features Not Understood
<!-- Format:
- [ ] Feature | What was confusing | Resolution
-->
- [x] Unity JsonUtility limitations | Cannot deserialize dynamic/unknown fields | Workaround: Manual JSON parsing with JsonRpcParamsParser

---

## Architecture Changes Made

### Parameter Parsing Fix (Critical)
The original architecture used Unity's `JsonUtility.FromJson<JsonRpcRequest>()` to parse incoming JSON-RPC messages. However, `JsonRpcParams` only had a `uri` field defined, and JsonUtility ignores unknown fields.

**Solution**: Created `JsonRpcParamsParser` class that:
1. Extracts the raw "params" JSON from the message string
2. Parses it manually into a `Dictionary<string, string>`
3. Handles nested objects, strings, numbers, and booleans
4. Updated all handlers to use `string paramsJson` instead of `object @params`

Files changed:
- `McpUnityBridge.cs` - Added JsonRpcParamsParser, updated interfaces
- All handler files - Changed Handle method signature, removed duplicate ParseParams methods

---

## Development Log

### Attempt 1
- Starting time: 2026-01-15
- Goal: Create simple iOS landscape game with UI controls
- Status: Successful (scene created, game logic implemented)

**Progress:**
1. Created Unity project at `C:\Users\joelw\Documents\UnityProjects\iOSGame`
2. Fixed MCP server auto-start issues
3. Fixed critical parameter parsing bug (all tools were failing)
4. Created GameScene with:
   - Canvas configured for landscape (1920x1080 reference)
   - ScoreText at top
   - GameArea with Paddle
   - Left/Right buttons for touch controls
5. Implemented GameController.cs - "Catch the Falling Objects" game
6. Configured for landscape orientation
7. Tested in play mode - no errors

**Remaining:**
- iOS Build Support not installed (user needs to install via Unity Hub)
- Cannot test actual build without iOS platform support

---

## Recommendations for MCP Server Improvements

### High Priority
1. **Component Reference Setting**: Add ability to set object references (e.g., `scoreText = Canvas/ScoreText`)
2. **RectTransform Manipulation**: Full RectTransform support (anchors, pivot, sizeDelta, offsetMin/Max)
3. **Better Error Messages**: Include the actual exception message in tool responses

### Medium Priority
1. **Scene Saving**: Tool to save current scene
2. **Asset Import Status**: Check if AssetDatabase is still importing
3. **Compilation Status**: Check if scripts are currently compiling

### Low Priority
1. **Undo System Integration**: Better Undo.RecordObject usage
2. **Prefab Editing**: Open prefab in isolation mode
3. **Animation Tools**: Create/edit animations

---

## Files Created/Modified This Session

### Created
- `C:\Users\joelw\Documents\UnityProjects\iOSGame\Assets\Scripts\GameController.cs`
- `C:\Users\joelw\Documents\UnityProjects\iOSGame\Assets\Editor\GameSceneSetup.cs`
- `C:\Users\joelw\Documents\UnityProjects\iOSGame\Assets\Editor\IOSSetup.cs`

### Modified (MCP Plugin)
- `McpUnityBridge.cs` - Added JsonRpcParamsParser, fixed parameter parsing
- All handler files - Updated to use new parsing
- `McpSettings.cs` - Changed AutoStart default to true
