using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Scene-related MCP tool requests.
    /// </summary>
    public class SceneHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "create_scene",
            "delete_scene",
            "load_scene"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "create_scene":
                    return CreateScene(paramsDict);
                case "delete_scene":
                    return DeleteScene(paramsDict);
                case "load_scene":
                    return LoadScene(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object CreateScene(Dictionary<string, string> @params)
        {
            var sceneName = @params.GetValueOrDefault("sceneName");
            var folderPath = @params.GetValueOrDefault("folderPath") ?? "Assets/Scenes";
            var makeActive = @params.GetValueOrDefault("makeActive")?.ToLower() != "false";
            var addToBuildSettings = @params.GetValueOrDefault("addToBuildSettings")?.ToLower() != "false";

            if (string.IsNullOrEmpty(sceneName))
            {
                return new { success = false, error = "sceneName is required" };
            }

            // Ensure folder exists
            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }

            var scenePath = $"{folderPath}/{sceneName}.unity";

            // Check if scene already exists
            if (System.IO.File.Exists(scenePath))
            {
                return new { success = false, error = $"Scene already exists: {scenePath}" };
            }

            // Auto-save current scene before creating a new single scene
            if (makeActive)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, makeActive ? NewSceneMode.Single : NewSceneMode.Additive);

            // Save the scene
            EditorSceneManager.SaveScene(newScene, scenePath);

            // Add to build settings if requested
            if (addToBuildSettings)
            {
                var buildScenes = EditorBuildSettings.scenes.ToList();
                buildScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            return new
            {
                success = true,
                scenePath = scenePath,
                sceneName = sceneName,
                addedToBuildSettings = addToBuildSettings
            };
        }

        private object DeleteScene(Dictionary<string, string> @params)
        {
            var scenePath = @params.GetValueOrDefault("scenePath");
            var sceneName = @params.GetValueOrDefault("sceneName");
            var folderPath = @params.GetValueOrDefault("folderPath");

            // Build full path if needed
            if (string.IsNullOrEmpty(scenePath))
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    return new { success = false, error = "scenePath or sceneName is required" };
                }

                var folder = folderPath ?? "Assets/Scenes";
                scenePath = $"{folder}/{sceneName}.unity";
            }

            if (!System.IO.File.Exists(scenePath))
            {
                return new { success = false, error = $"Scene not found: {scenePath}" };
            }

            // Remove from build settings
            var buildScenes = EditorBuildSettings.scenes.ToList();
            buildScenes.RemoveAll(s => s.path == scenePath);
            EditorBuildSettings.scenes = buildScenes.ToArray();

            // Delete the asset
            AssetDatabase.DeleteAsset(scenePath);

            return new
            {
                success = true,
                message = $"Deleted scene: {scenePath}"
            };
        }

        private object LoadScene(Dictionary<string, string> @params)
        {
            var scenePath = @params.GetValueOrDefault("scenePath");
            var sceneName = @params.GetValueOrDefault("sceneName");
            var folderPath = @params.GetValueOrDefault("folderPath");
            var additive = @params.GetValueOrDefault("additive")?.ToLower() == "true";

            // Build full path if needed
            if (string.IsNullOrEmpty(scenePath))
            {
                if (string.IsNullOrEmpty(sceneName))
                {
                    return new { success = false, error = "scenePath or sceneName is required" };
                }

                var folder = folderPath ?? "Assets/Scenes";
                scenePath = $"{folder}/{sceneName}.unity";
            }

            if (!System.IO.File.Exists(scenePath))
            {
                // Try to find scene by name in all folders
                var guids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
                if (guids.Length > 0)
                {
                    scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                }
                else
                {
                    return new { success = false, error = $"Scene not found: {scenePath}" };
                }
            }

            // Auto-save current scene before loading a new one
            if (!additive)
            {
                EditorSceneManager.SaveOpenScenes();
            }

            var mode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
            var loadedScene = EditorSceneManager.OpenScene(scenePath, mode);

            return new
            {
                success = true,
                scenePath = scenePath,
                sceneName = loadedScene.name,
                additive = additive
            };
        }
    }
}
