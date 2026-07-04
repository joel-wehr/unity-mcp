using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Editor-related MCP tool requests (menu items, play mode, recompile).
    /// </summary>
    public class EditorHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "execute_menu_item",
            "play_mode",
            "recompile_scripts"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "execute_menu_item":
                    return ExecuteMenuItem(paramsDict);
                case "play_mode":
                    return HandlePlayMode(paramsDict);
                case "recompile_scripts":
                    return RecompileScripts(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object ExecuteMenuItem(Dictionary<string, string> @params)
        {
            var menuPath = @params.GetValueOrDefault("menuPath");

            if (string.IsNullOrEmpty(menuPath))
            {
                return new { success = false, error = "menuPath is required" };
            }

            var executed = EditorApplication.ExecuteMenuItem(menuPath);

            return new
            {
                success = executed,
                menuPath = menuPath,
                message = executed ? $"Executed: {menuPath}" : $"Menu item not found: {menuPath}"
            };
        }

        private object HandlePlayMode(Dictionary<string, string> @params)
        {
            var action = @params.GetValueOrDefault("action");

            if (string.IsNullOrEmpty(action))
            {
                return new { success = false, error = "action is required" };
            }

            switch (action.ToLower())
            {
                case "enter":
                    if (EditorApplication.isPlaying)
                    {
                        return new { success = true, message = "Already in play mode", state = "Playing" };
                    }
                    EditorApplication.isPlaying = true;
                    return new { success = true, message = "Entering play mode", state = "Playing" };

                case "exit":
                    if (!EditorApplication.isPlaying)
                    {
                        return new { success = true, message = "Not in play mode", state = "Stopped" };
                    }
                    EditorApplication.isPlaying = false;
                    return new { success = true, message = "Exiting play mode", state = "Stopped" };

                case "pause":
                    if (!EditorApplication.isPlaying)
                    {
                        return new { success = false, error = "Cannot pause when not in play mode" };
                    }
                    EditorApplication.isPaused = true;
                    return new { success = true, message = "Paused", state = "Paused" };

                case "unpause":
                    if (!EditorApplication.isPlaying)
                    {
                        return new { success = false, error = "Cannot unpause when not in play mode" };
                    }
                    EditorApplication.isPaused = false;
                    return new { success = true, message = "Unpaused", state = "Playing" };

                case "step":
                    if (!EditorApplication.isPlaying || !EditorApplication.isPaused)
                    {
                        return new { success = false, error = "Can only step when paused in play mode" };
                    }
                    EditorApplication.Step();
                    return new { success = true, message = "Stepped one frame", state = "Paused" };

                case "get_state":
                    var state = "Stopped";
                    if (EditorApplication.isPlaying)
                    {
                        state = EditorApplication.isPaused ? "Paused" : "Playing";
                    }
                    return new
                    {
                        success = true,
                        state = state,
                        isPlaying = EditorApplication.isPlaying,
                        isPaused = EditorApplication.isPaused,
                        isCompiling = EditorApplication.isCompiling
                    };

                default:
                    return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object RecompileScripts(Dictionary<string, string> @params)
        {
            var returnWithLogs = @params.GetValueOrDefault("returnWithLogs")?.ToLower() != "false";
            var logsLimitStr = @params.GetValueOrDefault("logsLimit") ?? "100";
            var logsLimit = int.TryParse(logsLimitStr, out var limit) ? limit : 100;

            // Clear previous compilation logs
            var logsBefore = new List<string>();

            // Request script compilation
            AssetDatabase.Refresh();
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

            // Wait for compilation to start and complete
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(30);

            while (!EditorApplication.isCompiling && (DateTime.Now - startTime) < TimeSpan.FromSeconds(2))
            {
                System.Threading.Thread.Sleep(100);
            }

            while (EditorApplication.isCompiling && (DateTime.Now - startTime) < timeout)
            {
                System.Threading.Thread.Sleep(100);
            }

            var hadErrors = EditorUtility.scriptCompilationFailed;

            var result = new
            {
                success = !hadErrors,
                compilationTime = (DateTime.Now - startTime).TotalSeconds,
                hadErrors = hadErrors,
                message = hadErrors ? "Compilation failed with errors" : "Compilation successful"
            };

            return result;
        }
    }
}
