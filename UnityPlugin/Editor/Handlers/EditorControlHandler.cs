using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class EditorControlHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "editor_control" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "focus_window": return FocusWindow(p);
                case "get_windows": return GetWindows();
                case "open_window": return OpenWindow(p);
                case "close_window": return CloseWindow(p);
                case "ping_asset": return PingAsset(p);
                case "frame_selected": return FrameSelected();
                case "get_editor_state": return GetEditorState();
                case "refresh": return Refresh();
                case "clear_console": return ClearConsole();
                case "take_screenshot": return TakeScreenshot(p);
                case "set_scene_view": return SetSceneView(p);
                case "inspector_lock": return InspectorLock(p);
                case "inspector_mode": return InspectorMode(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object FocusWindow(Dictionary<string, string> p)
        {
            var windowType = p.GetValueOrDefault("windowType") ?? "Game";
            var type = GetWindowType(windowType);
            if (type == null) return new { success = false, error = $"Unknown window type: {windowType}" };
            EditorWindow.GetWindow(type).Focus();
            return new { success = true, message = $"Focused {windowType} window" };
        }

        private object GetWindows()
        {
            var windows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();
            var list = windows.Select(w => new
            {
                title = w.titleContent.text,
                type = w.GetType().Name,
                focused = w.hasFocus,
                position = new { x = w.position.x, y = w.position.y, w = w.position.width, h = w.position.height }
            }).ToList();
            return new { success = true, windows = list };
        }

        private object OpenWindow(Dictionary<string, string> p)
        {
            var windowType = p.GetValueOrDefault("windowType") ?? "Inspector";
            var type = GetWindowType(windowType);
            if (type == null) return new { success = false, error = $"Unknown window type: {windowType}" };
            EditorWindow.GetWindow(type).Show();
            return new { success = true, message = $"Opened {windowType}" };
        }

        private object CloseWindow(Dictionary<string, string> p)
        {
            var windowType = p.GetValueOrDefault("windowType");
            var indexStr = p.GetValueOrDefault("windowIndex");
            if (string.IsNullOrEmpty(windowType))
                return new { success = false, error = "windowType is required" };

            var type = GetWindowType(windowType);
            if (type == null) return new { success = false, error = $"Unknown window type: {windowType}" };

            var windows = UnityEngine.Resources.FindObjectsOfTypeAll(type).OfType<EditorWindow>().ToArray();
            var index = int.TryParse(indexStr, out var i) ? i : 0;
            if (index < windows.Length)
            {
                windows[index].Close();
                return new { success = true, message = $"Closed {windowType}" };
            }
            return new { success = false, error = $"Window index {index} out of range (found {windows.Length})" };
        }

        private object PingAsset(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (obj == null) return new { success = false, error = $"Asset not found: {assetPath}" };

            EditorGUIUtility.PingObject(obj);
            return new { success = true, message = $"Pinged {assetPath}" };
        }

        private object FrameSelected()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.FrameSelected();
                return new { success = true, message = "Framed selection in Scene view" };
            }
            return new { success = false, error = "No active Scene view" };
        }

        private object GetEditorState()
        {
            return new
            {
                success = true,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                activeScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount,
                timeSinceStartup = EditorApplication.timeSinceStartup,
                applicationPath = EditorApplication.applicationPath,
                applicationVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                projectPath = Application.dataPath,
                selectedObjects = Selection.objects?.Select(o => o.name).ToArray() ?? Array.Empty<string>()
            };
        }

        private object Refresh()
        {
            AssetDatabase.Refresh();
            return new { success = true, message = "AssetDatabase refreshed" };
        }

        private object ClearConsole()
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntries != null)
            {
                var clear = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
                clear?.Invoke(null, null);
                return new { success = true, message = "Console cleared" };
            }
            return new { success = false, error = "Could not access LogEntries.Clear" };
        }

        private object TakeScreenshot(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("screenshotPath");
            if (string.IsNullOrEmpty(path))
            {
                var dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                path = System.IO.Path.Combine(dir, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }

            // Use Game view capture
            ScreenCapture.CaptureScreenshot(path);
            return new
            {
                success = true,
                message = $"Screenshot saved to {path}",
                path = path
            };
        }

        private object SetSceneView(Dictionary<string, string> p)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new { success = false, error = "No active Scene view" };

            if (p.TryGetValue("sceneViewSettings", out var settingsJson))
            {
                var settings = JsonRpcParamsParser.ParseToDictionary(settingsJson);
                if (settings.TryGetValue("orthographic", out var ortho))
                    sceneView.orthographic = ortho.ToLower() == "true";
                if (settings.TryGetValue("size", out var sizeStr) && float.TryParse(sizeStr, out var size))
                    sceneView.size = size;

                sceneView.Repaint();
            }

            return new
            {
                success = true,
                orthographic = sceneView.orthographic,
                size = sceneView.size,
                pivot = new { x = sceneView.pivot.x, y = sceneView.pivot.y, z = sceneView.pivot.z }
            };
        }

        private object InspectorLock(Dictionary<string, string> p)
        {
            var locked = p.GetValueOrDefault("locked")?.ToLower() == "true";
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var inspector = EditorWindow.GetWindow(inspectorType);
            if (inspector != null)
            {
                var isLockedProp = inspectorType.GetProperty("isLocked");
                isLockedProp?.SetValue(inspector, locked);
                return new { success = true, locked = locked };
            }
            return new { success = false, error = "Inspector window not found" };
        }

        private object InspectorMode(Dictionary<string, string> p)
        {
            var debug = p.GetValueOrDefault("debugMode")?.ToLower() == "true";
            var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var inspector = EditorWindow.GetWindow(inspectorType);
            if (inspector != null)
            {
                var setModeMethod = inspectorType.GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);
                setModeMethod?.Invoke(inspector, new object[] { debug ? 1 : 0 });
                return new { success = true, debugMode = debug };
            }
            return new { success = false, error = "Inspector window not found" };
        }

        private Type GetWindowType(string name)
        {
            switch (name.ToLower())
            {
                case "game": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                case "scene": return typeof(SceneView);
                case "inspector": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                case "hierarchy": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                case "project": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
                case "console": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ConsoleWindow");
                case "animation": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnimationWindow");
                case "profiler": return typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProfilerWindow");
                default: return Type.GetType($"UnityEditor.{name}, UnityEditor");
            }
        }
    }
}
