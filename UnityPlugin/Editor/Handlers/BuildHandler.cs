using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Build Pipeline MCP tool requests.
    /// </summary>
    public class BuildHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "build_pipeline"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "build_pipeline":
                    return HandleBuildPipeline(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object HandleBuildPipeline(Dictionary<string, string> @params)
        {
            var action = @params.GetValueOrDefault("action");

            if (string.IsNullOrEmpty(action))
            {
                return new { success = false, error = "action is required" };
            }

            switch (action.ToLower())
            {
                case "switch_platform":
                    return SwitchPlatform(@params);
                case "get_platforms":
                    return GetPlatforms();
                case "get_settings":
                    return GetBuildSettings();
                case "get_scenes":
                    return GetScenes();
                case "set_scenes":
                    return SetScenes(@params);
                case "build":
                    return ExecuteBuild(@params);
                case "get_player_settings":
                    return GetPlayerSettings(@params);
                case "set_player_settings":
                    return SetPlayerSettings(@params);
                case "validate":
                    return ValidateBuild();
                case "get_report":
                    return GetLastBuildReport();
                default:
                    return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object SwitchPlatform(Dictionary<string, string> @params)
        {
            var platformStr = @params.GetValueOrDefault("platform");

            if (string.IsNullOrEmpty(platformStr))
            {
                return new { success = false, error = "platform is required" };
            }

            BuildTarget target;
            BuildTargetGroup targetGroup;

            switch (platformStr.ToLower())
            {
                case "ios":
                    target = BuildTarget.iOS;
                    targetGroup = BuildTargetGroup.iOS;
                    break;
                case "android":
                    target = BuildTarget.Android;
                    targetGroup = BuildTargetGroup.Android;
                    break;
                case "standalonewindows64":
                    target = BuildTarget.StandaloneWindows64;
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case "standaloneosx":
                    target = BuildTarget.StandaloneOSX;
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case "standalonelinux64":
                    target = BuildTarget.StandaloneLinux64;
                    targetGroup = BuildTargetGroup.Standalone;
                    break;
                case "webgl":
                    target = BuildTarget.WebGL;
                    targetGroup = BuildTargetGroup.WebGL;
                    break;
                default:
                    return new { success = false, error = $"Unknown platform: {platformStr}" };
            }

            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                return new
                {
                    success = true,
                    message = $"Already on platform: {platformStr}",
                    platform = platformStr,
                    buildTarget = target.ToString()
                };
            }

            var result = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);

            return new
            {
                success = result,
                message = result ? $"Switched to platform: {platformStr}" : $"Failed to switch to platform: {platformStr}",
                platform = platformStr,
                buildTarget = target.ToString(),
                previousTarget = EditorUserBuildSettings.activeBuildTarget.ToString()
            };
        }

        private object GetPlatforms()
        {
            var platforms = new List<object>
            {
                new { name = "iOS", buildTarget = "iOS", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS) },
                new { name = "Android", buildTarget = "Android", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android) },
                new { name = "StandaloneWindows64", buildTarget = "StandaloneWindows64", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64) },
                new { name = "StandaloneOSX", buildTarget = "StandaloneOSX", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX) },
                new { name = "StandaloneLinux64", buildTarget = "StandaloneLinux64", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64) },
                new { name = "WebGL", buildTarget = "WebGL", installed = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL) }
            };

            return new
            {
                success = true,
                activePlatform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                platforms = platforms
            };
        }

        private object GetBuildSettings()
        {
            return new
            {
                success = true,
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                development = EditorUserBuildSettings.development,
                connectProfiler = EditorUserBuildSettings.connectProfiler,
                allowDebugging = EditorUserBuildSettings.allowDebugging,
                buildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                compressWithLz4 = PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString()
            };
        }

        private object GetScenes()
        {
            var scenes = EditorBuildSettings.scenes.Select((s, i) => new
            {
                index = i,
                path = s.path,
                enabled = s.enabled,
                guid = s.guid.ToString()
            }).ToList();

            return new
            {
                success = true,
                sceneCount = scenes.Count,
                scenes = scenes
            };
        }

        private object SetScenes(Dictionary<string, string> @params)
        {
            // Expect a "scenes" param holding a JSON array: [{"path":"...","enabled":true}, ...].
            // JsonRpcParamsParser preserves arrays/objects as their raw JSON substring, so we
            // parse the entries manually here (Unity's JsonUtility doesn't deal with dynamic arrays).
            var scenesJson = @params.GetValueOrDefault("scenes");
            if (string.IsNullOrWhiteSpace(scenesJson))
                return new { success = false, error = "scenes parameter is required (array of { path, enabled })" };

            var trimmed = scenesJson.Trim();
            if (!trimmed.StartsWith("["))
                return new { success = false, error = "scenes must be a JSON array" };

            var entries = ParseSceneArray(trimmed);
            var newScenes = new List<EditorBuildSettingsScene>();
            var skipped = new List<string>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.path))
                {
                    skipped.Add("(entry missing path)");
                    continue;
                }
                // Allow callers to pass absolute paths or paths relative to the project — normalize
                // to the "Assets/..." form Unity expects, when possible.
                var path = entry.path.Replace('\\', '/');
                if (!path.StartsWith("Assets/") && System.IO.Path.IsPathRooted(path))
                {
                    var projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(projectRoot) && path.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        path = path.Substring(projectRoot.Length + 1);
                    }
                }
                newScenes.Add(new EditorBuildSettingsScene(path, entry.enabled));
            }

            EditorBuildSettings.scenes = newScenes.ToArray();
            AssetDatabase.SaveAssets();

            return new
            {
                success = true,
                sceneCount = newScenes.Count,
                enabledCount = newScenes.Count(s => s.enabled),
                scenes = newScenes.Select((s, i) => new
                {
                    index = i,
                    path = s.path,
                    enabled = s.enabled
                }).ToList(),
                skipped = skipped.Count > 0 ? skipped : null
            };
        }

        private struct SceneEntry { public string path; public bool enabled; }

        // Lightweight scanner for [{"path":"...","enabled":true}, ...]. Keeps us out of JsonUtility,
        // which can't deserialize arrays of dynamic objects.
        private static List<SceneEntry> ParseSceneArray(string array)
        {
            var result = new List<SceneEntry>();
            var i = 1; // skip leading '['
            while (i < array.Length)
            {
                while (i < array.Length && (char.IsWhiteSpace(array[i]) || array[i] == ',')) i++;
                if (i >= array.Length || array[i] == ']') break;
                if (array[i] != '{') { i++; continue; }

                // Find the matching '}'.
                var braceDepth = 0;
                var objStart = i;
                var inStr = false;
                var esc = false;
                while (i < array.Length)
                {
                    var c = array[i];
                    if (esc) { esc = false; i++; continue; }
                    if (c == '\\') { esc = true; i++; continue; }
                    if (c == '"') { inStr = !inStr; i++; continue; }
                    if (!inStr)
                    {
                        if (c == '{') braceDepth++;
                        else if (c == '}')
                        {
                            braceDepth--;
                            if (braceDepth == 0) { i++; break; }
                        }
                    }
                    i++;
                }
                var objJson = array.Substring(objStart, i - objStart);
                var objDict = JsonRpcParamsParser.ParseToDictionary(objJson);
                var path = objDict.GetValueOrDefault("path", "");
                var enabledStr = objDict.GetValueOrDefault("enabled", "true");
                var enabled = string.Equals(enabledStr, "true", StringComparison.OrdinalIgnoreCase)
                    || enabledStr == "1";
                result.Add(new SceneEntry { path = path, enabled = enabled });
            }
            return result;
        }

        private object ExecuteBuild(Dictionary<string, string> @params)
        {
            var buildPath = @params.GetValueOrDefault("buildPath");
            var development = @params.GetValueOrDefault("development")?.ToLower() == "true";

            if (string.IsNullOrEmpty(buildPath))
            {
                // Default build paths based on platform
                var target = EditorUserBuildSettings.activeBuildTarget;
                switch (target)
                {
                    case BuildTarget.iOS:
                        buildPath = "Builds/iOS";
                        break;
                    case BuildTarget.Android:
                        buildPath = "Builds/Android/game.apk";
                        break;
                    case BuildTarget.StandaloneWindows64:
                        buildPath = "Builds/Windows/game.exe";
                        break;
                    default:
                        buildPath = "Builds/output";
                        break;
                }
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                return new { success = false, error = "No scenes in build settings" };
            }

            var options = BuildOptions.None;
            if (development)
            {
                options |= BuildOptions.Development;
            }

            var report = BuildPipeline.BuildPlayer(scenes, buildPath, EditorUserBuildSettings.activeBuildTarget, options);

            return new
            {
                success = report.summary.result == BuildResult.Succeeded,
                result = report.summary.result.ToString(),
                outputPath = report.summary.outputPath,
                totalSize = report.summary.totalSize,
                totalTime = report.summary.totalTime.TotalSeconds,
                totalErrors = report.summary.totalErrors,
                totalWarnings = report.summary.totalWarnings
            };
        }

        private object GetPlayerSettings(Dictionary<string, string> @params)
        {
            var platform = @params.GetValueOrDefault("platform") ?? EditorUserBuildSettings.activeBuildTarget.ToString();

            return new
            {
                success = true,
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                defaultIsFullScreen = PlayerSettings.fullScreenMode.ToString(),
                runInBackground = PlayerSettings.runInBackground,
                iOS = new
                {
                    bundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
                    buildNumber = PlayerSettings.iOS.buildNumber,
                    targetDevice = PlayerSettings.iOS.targetDevice.ToString(),
                    targetOSVersionString = PlayerSettings.iOS.targetOSVersionString,
                    requiresPersistentWiFi = PlayerSettings.iOS.requiresPersistentWiFi,
                    allowHTTPDownload = PlayerSettings.iOS.allowHTTPDownload,
                    requiresFullScreen = PlayerSettings.iOS.requiresFullScreen,
                    statusBarHidden = PlayerSettings.iOS.hideHomeButton,
                    supportedOrientations = new
                    {
                        portrait = PlayerSettings.allowedAutorotateToPortrait,
                        portraitUpsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                        landscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft,
                        landscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight
                    }
                }
            };
        }

        private object SetPlayerSettings(Dictionary<string, string> @params)
        {
            var changed = new List<string>();

            if (@params.TryGetValue("companyName", out var companyName))
            {
                PlayerSettings.companyName = companyName;
                changed.Add("companyName");
            }

            if (@params.TryGetValue("productName", out var productName))
            {
                PlayerSettings.productName = productName;
                changed.Add("productName");
            }

            if (@params.TryGetValue("bundleVersion", out var bundleVersion))
            {
                PlayerSettings.bundleVersion = bundleVersion;
                changed.Add("bundleVersion");
            }

            if (@params.TryGetValue("bundleIdentifier", out var bundleId))
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, bundleId);
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, bundleId);
                changed.Add("bundleIdentifier");
            }

            // iOS-specific settings
            if (@params.TryGetValue("defaultOrientation", out var orientation))
            {
                switch (orientation.ToLower())
                {
                    case "landscape":
                    case "landscapeleft":
                        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                        break;
                    case "landscaperight":
                        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeRight;
                        break;
                    case "portrait":
                        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                        break;
                    case "portraitupsidedown":
                        PlayerSettings.defaultInterfaceOrientation = UIOrientation.PortraitUpsideDown;
                        break;
                    case "autorotation":
                        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                        break;
                }
                changed.Add("defaultOrientation");
            }

            // Set landscape-only orientations
            if (@params.TryGetValue("landscapeOnly", out var landscapeOnly) && landscapeOnly.ToLower() == "true")
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
                changed.Add("landscapeOnly orientation settings");
            }

            return new
            {
                success = true,
                changed = changed,
                message = $"Updated {changed.Count} player settings"
            };
        }

        private object GetLastBuildReport()
        {
            // Unity doesn't persist build reports, but we can read the last build log
            var logPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath),
                "Library", "LastBuild.buildreport"
            );

            if (!System.IO.File.Exists(logPath))
            {
                return new
                {
                    success = true,
                    hasReport = false,
                    message = "No build report found. Run a build first.",
                    currentSettings = new
                    {
                        platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                        development = EditorUserBuildSettings.development,
                        sceneCount = EditorBuildSettings.scenes.Count(s => s.enabled)
                    }
                };
            }

            // Load the build report asset
            var report = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Library/LastBuild.buildreport");

            return new
            {
                success = true,
                hasReport = true,
                message = "Build report exists. Use the 'build' action to get a fresh report with full details.",
                reportPath = logPath,
                currentSettings = new
                {
                    platform = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    development = EditorUserBuildSettings.development,
                    sceneCount = EditorBuildSettings.scenes.Count(s => s.enabled)
                }
            };
        }

        private object ValidateBuild()
        {
            var issues = new List<string>();

            // Check if there are scenes in build settings
            if (EditorBuildSettings.scenes.Length == 0)
            {
                issues.Add("No scenes in build settings");
            }
            else if (!EditorBuildSettings.scenes.Any(s => s.enabled))
            {
                issues.Add("No enabled scenes in build settings");
            }

            // Check if current platform is supported
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                issues.Add($"Build target {target} is not installed");
            }

            // Check for basic player settings
            if (string.IsNullOrEmpty(PlayerSettings.productName))
            {
                issues.Add("Product name is not set");
            }

            if (string.IsNullOrEmpty(PlayerSettings.companyName))
            {
                issues.Add("Company name is not set");
            }

            return new
            {
                success = issues.Count == 0,
                isValid = issues.Count == 0,
                platform = target.ToString(),
                sceneCount = EditorBuildSettings.scenes.Count(s => s.enabled),
                issues = issues.Count > 0 ? issues : null,
                message = issues.Count == 0 ? "Build configuration is valid" : $"Found {issues.Count} issue(s)"
            };
        }
    }
}
