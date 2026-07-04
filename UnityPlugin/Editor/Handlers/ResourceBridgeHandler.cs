using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Bridges TS resource requests to C# resource/tool handlers.
    /// TS resources send method names like "get_scenes_hierarchy" that don't match
    /// any tool handler's SupportedMethods. This handler registers those method names
    /// and delegates to the appropriate resource handler or tool handler.
    /// </summary>
    public class ResourceBridgeHandler : IToolHandler
    {
        // Existing resource handler instances for delegation
        private readonly Resources.SceneHierarchyResource _sceneHierarchyResource = new Resources.SceneHierarchyResource();
        private readonly Resources.AssetsResource _assetsResource = new Resources.AssetsResource();
        private readonly Resources.PackagesResource _packagesResource = new Resources.PackagesResource();

        // Existing tool handler instances for delegation
        private readonly EditorControlHandler _editorControlHandler = new EditorControlHandler();
        private readonly ProfilerHandler _profilerHandler = new ProfilerHandler();
        private readonly BuildHandler _buildHandler = new BuildHandler();
        private readonly ProjectSettingsHandler _projectSettingsHandler = new ProjectSettingsHandler();

        // Xreal resource handler instances
        private readonly Resources.Xreal.DeviceStateResource _xrealDeviceResource = new Resources.Xreal.DeviceStateResource();
        private readonly Resources.Xreal.HandTrackingResource _xrealHandTrackingResource = new Resources.Xreal.HandTrackingResource();
        private readonly Resources.Xreal.SpatialAnchorsResource _xrealSpatialAnchorsResource = new Resources.Xreal.SpatialAnchorsResource();
        private readonly Resources.Xreal.DetectedPlanesResource _xrealDetectedPlanesResource = new Resources.Xreal.DetectedPlanesResource();
        private readonly Resources.Xreal.TrackedImagesResource _xrealTrackedImagesResource = new Resources.Xreal.TrackedImagesResource();
        private readonly Resources.Xreal.BuildSettingsResource _xrealBuildSettingsResource = new Resources.Xreal.BuildSettingsResource();

        public string[] SupportedMethods => new[]
        {
            // Core resource methods (no matching tool handler)
            "get_scenes_hierarchy",
            "get_assets",
            "get_packages",
            "get_menu_items",
            "get_tests",

            // Tool-based resource methods (delegate to tool handlers with action params)
            "get_editor_state",
            "get_profiler_data",
            "get_build_info",
            "get_project_settings",

            // Xreal resource methods
            "get_xreal_device_state",
            "get_hand_tracking_state",
            "get_spatial_anchors",
            "get_detected_planes_resource",
            "get_tracked_images_resource",
            "get_xreal_build_settings"
        };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                // === Core resource delegates ===
                case "get_scenes_hierarchy":
                    return _sceneHierarchyResource.Handle("unity://scenes_hierarchy", paramsJson);

                case "get_assets":
                    return _assetsResource.Handle("unity://assets", paramsJson);

                case "get_packages":
                    return _packagesResource.Handle("unity://packages", paramsJson);

                case "get_menu_items":
                    return GetMenuItems();

                case "get_tests":
                    return GetTests(p);

                // === Tool handler delegates (with action param injection) ===
                case "get_editor_state":
                    return _editorControlHandler.Handle("editor_control",
                        "{\"action\":\"get_editor_state\"}");

                case "get_profiler_data":
                    return HandleProfilerData(p);

                case "get_build_info":
                    return HandleBuildInfo(p);

                case "get_project_settings":
                    return HandleProjectSettings(p);

                // === Xreal resource delegates ===
                case "get_xreal_device_state":
                    return _xrealDeviceResource.Handle("xreal://device_state", paramsJson);

                case "get_hand_tracking_state":
                    return _xrealHandTrackingResource.Handle("xreal://hand_tracking", paramsJson);

                case "get_spatial_anchors":
                    return _xrealSpatialAnchorsResource.Handle("xreal://spatial_anchors", paramsJson);

                case "get_detected_planes_resource":
                    return _xrealDetectedPlanesResource.Handle("xreal://detected_planes", paramsJson);

                case "get_tracked_images_resource":
                    return _xrealTrackedImagesResource.Handle("xreal://tracked_images", paramsJson);

                case "get_xreal_build_settings":
                    return _xrealBuildSettingsResource.Handle("xreal://build_settings", paramsJson);

                default:
                    return new { success = false, error = $"Unknown resource bridge method: {method}" };
            }
        }

        private object HandleProfilerData(Dictionary<string, string> p)
        {
            var dataType = p.GetValueOrDefault("dataType") ?? "frame_timing";

            // Map resource dataType names to profiler action names
            string profilerAction;
            switch (dataType.ToLower())
            {
                case "frame_timing": profilerAction = "get_frame_data"; break;
                case "memory": profilerAction = "get_memory_snapshot"; break;
                case "rendering": profilerAction = "get_render_stats"; break;
                case "cpu": profilerAction = "get_cpu_usage"; break;
                case "gc_allocs": profilerAction = "get_gc_allocs"; break;
                case "physics":
                    // Physics stats aren't in the profiler — return basic physics info
                    return new
                    {
                        success = true,
                        dataType = "physics",
                        gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                        defaultSolverIterations = Physics.defaultSolverIterations,
                        simulationMode = Physics.simulationMode.ToString()
                    };
                default: profilerAction = "get_frame_data"; break;
            }

            return _profilerHandler.Handle("profiler", $"{{\"action\":\"{profilerAction}\"}}");
        }

        private object HandleBuildInfo(Dictionary<string, string> p)
        {
            var infoType = p.GetValueOrDefault("infoType") ?? "settings";

            // Map resource infoType names to build_pipeline action names
            string buildAction;
            switch (infoType.ToLower())
            {
                case "settings": buildAction = "get_settings"; break;
                case "scenes": buildAction = "get_scenes"; break;
                case "platforms": buildAction = "get_platforms"; break;
                case "report": buildAction = "validate"; break; // closest match
                default: buildAction = "get_settings"; break;
            }

            return _buildHandler.Handle("build_pipeline", $"{{\"action\":\"{buildAction}\"}}");
        }

        private object HandleProjectSettings(Dictionary<string, string> p)
        {
            var category = p.GetValueOrDefault("category") ?? "player";
            return _projectSettingsHandler.Handle("project_settings",
                $"{{\"action\":\"get\",\"category\":\"{category}\"}}");
        }

        private object GetMenuItems()
        {
            var menuItems = new List<object>();

            // Use Unsupported.GetSubmenus to enumerate menu items
            try
            {
                var unsupportedType = typeof(EditorApplication).Assembly.GetType("UnityEditor.Unsupported");
                if (unsupportedType != null)
                {
                    var getSubmenus = unsupportedType.GetMethod("GetSubmenus",
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    if (getSubmenus != null)
                    {
                        var menus = getSubmenus.Invoke(null, new object[] { "MainMenu" }) as string[];
                        if (menus != null)
                        {
                            foreach (var menu in menus)
                            {
                                menuItems.Add(new { path = menu });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Error enumerating menu items: {ex.Message}");
            }

            // Fallback: add common known menu paths if reflection failed
            if (menuItems.Count == 0)
            {
                var commonMenus = new[]
                {
                    "File/New Scene", "File/Open Scene", "File/Save", "File/Save As...",
                    "File/Build Settings...", "File/Build And Run",
                    "Edit/Undo", "Edit/Redo", "Edit/Play", "Edit/Pause", "Edit/Step",
                    "Edit/Project Settings...", "Edit/Preferences...",
                    "Assets/Create/Material", "Assets/Create/Shader/Standard Surface Shader",
                    "Assets/Create/C# Script", "Assets/Create/Folder",
                    "Assets/Refresh", "Assets/Reimport All",
                    "GameObject/Create Empty", "GameObject/3D Object/Cube",
                    "GameObject/3D Object/Sphere", "GameObject/3D Object/Plane",
                    "GameObject/Light/Directional Light", "GameObject/Camera",
                    "GameObject/UI/Canvas", "GameObject/UI/Text - TextMeshPro",
                    "GameObject/UI/Button - TextMeshPro", "GameObject/UI/Image",
                    "Component/Add...",
                    "Window/General/Scene", "Window/General/Game",
                    "Window/General/Inspector", "Window/General/Hierarchy",
                    "Window/General/Project", "Window/General/Console",
                    "Window/Package Manager", "Window/Analysis/Profiler"
                };

                foreach (var menu in commonMenus)
                {
                    menuItems.Add(new { path = menu, note = "common_fallback" });
                }
            }

            return new
            {
                success = true,
                count = menuItems.Count,
                menuItems = menuItems
            };
        }

        private object GetTests(Dictionary<string, string> p)
        {
#if TEST_FRAMEWORK_ENABLED
            var testMode = p.GetValueOrDefault("testMode") ?? "";

            try
            {
                var api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
                var tests = new List<object>();

                // Retrieve test tree for the specified mode(s)
                var modes = new List<UnityEditor.TestTools.TestRunner.Api.TestMode>();

                if (string.IsNullOrEmpty(testMode) || testMode.ToLower() == "editmode")
                    modes.Add(UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode);
                if (string.IsNullOrEmpty(testMode) || testMode.ToLower() == "playmode")
                    modes.Add(UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode);

                foreach (var mode in modes)
                {
                    var filter = new UnityEditor.TestTools.TestRunner.Api.Filter { testMode = mode };
                    api.RetrieveTestList(mode, (testRoot) =>
                    {
                        CollectTests(testRoot, tests, mode.ToString());
                    });
                }

                UnityEngine.Object.DestroyImmediate(api);

                return new
                {
                    success = true,
                    testMode = string.IsNullOrEmpty(testMode) ? "all" : testMode,
                    count = tests.Count,
                    tests = tests
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = $"Failed to list tests: {ex.Message}" };
            }
#else
            return new
            {
                success = false,
                error = "Test Framework not available. Install com.unity.test-framework package.",
                tests = new object[0]
            };
#endif
        }

#if TEST_FRAMEWORK_ENABLED
        private void CollectTests(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test, List<object> tests, string mode)
        {
            if (test == null) return;

            if (!test.IsSuite)
            {
                tests.Add(new
                {
                    name = test.Name,
                    fullName = test.FullName,
                    testMode = mode,
                    runState = test.RunState.ToString()
                });
            }

            if (test.Children != null)
            {
                foreach (var child in test.Children)
                {
                    CollectTests(child, tests, mode);
                }
            }
        }
#endif
    }
}
