using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class ProjectSettingsHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "project_settings" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get": return GetSettings(p);
                case "set": return SetSettings(p);
                case "list_categories": return ListCategories();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object ListCategories()
        {
            return new
            {
                success = true,
                categories = new[]
                {
                    new { name = "player", description = "Player settings (company, product, icons, orientation)" },
                    new { name = "quality", description = "Quality settings (shadows, AA, rendering)" },
                    new { name = "physics", description = "Physics settings (gravity, timestep, layers)" },
                    new { name = "time", description = "Time settings (fixed timestep, max timestep)" },
                    new { name = "audio", description = "Audio settings (volume, spatializer)" },
                    new { name = "input", description = "Input settings (axes, input system)" },
                    new { name = "tags_layers", description = "Tags and sorting layers" },
                    new { name = "graphics", description = "Graphics settings (render pipeline, tiers)" },
                    new { name = "editor", description = "Editor settings (asset serialization, version control)" }
                }
            };
        }

        private object GetSettings(Dictionary<string, string> p)
        {
            var category = p.GetValueOrDefault("category") ?? "player";

            switch (category.ToLower())
            {
                case "player": return GetPlayerSettings();
                case "quality": return GetQualitySettings();
                case "physics": return GetPhysicsSettings();
                case "time": return GetTimeSettings();
                case "audio": return GetAudioSettings();
                case "tags_layers": return GetTagsAndLayers();
                case "graphics": return GetGraphicsSettings();
                case "editor": return GetEditorSettings();
                default: return new { success = false, error = $"Unknown category: {category}" };
            }
        }

        private object GetPlayerSettings()
        {
            return new
            {
                success = true,
                category = "player",
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                fullScreenMode = PlayerSettings.fullScreenMode.ToString(),
                defaultOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
                runInBackground = PlayerSettings.runInBackground,
                colorSpace = PlayerSettings.colorSpace.ToString(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                iOS = new
                {
                    bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
                    buildNumber = PlayerSettings.iOS.buildNumber,
                    targetOS = PlayerSettings.iOS.targetOSVersionString,
                    automaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning,
                    requiresFullScreen = PlayerSettings.iOS.requiresFullScreen
                },
                android = new
                {
                    bundleId = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
                    minSdkVersion = PlayerSettings.Android.minSdkVersion.ToString(),
                    targetSdkVersion = PlayerSettings.Android.targetSdkVersion.ToString()
                }
            };
        }

        private object GetQualitySettings()
        {
            return new
            {
                success = true,
                category = "quality",
                currentLevel = QualitySettings.GetQualityLevel(),
                names = QualitySettings.names,
                pixelLightCount = QualitySettings.pixelLightCount,
                antiAliasing = QualitySettings.antiAliasing,
                shadowDistance = QualitySettings.shadowDistance,
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                vSyncCount = QualitySettings.vSyncCount,
                lodBias = QualitySettings.lodBias,
                masterTextureLimit = QualitySettings.globalTextureMipmapLimit,
                anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString()
            };
        }

        private object GetPhysicsSettings()
        {
            return new
            {
                success = true,
                category = "physics",
                gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                defaultSolverIterations = Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                bounceThreshold = Physics.bounceThreshold,
                sleepThreshold = Physics.sleepThreshold,
                defaultContactOffset = Physics.defaultContactOffset,
                autoSimulation = Physics.simulationMode.ToString()
            };
        }

        private object GetTimeSettings()
        {
            return new
            {
                success = true,
                category = "time",
                fixedDeltaTime = Time.fixedDeltaTime,
                maximumDeltaTime = Time.maximumDeltaTime,
                timeScale = Time.timeScale,
                maximumParticleDeltaTime = Time.maximumParticleDeltaTime
            };
        }

        private object GetAudioSettings()
        {
            var config = AudioSettings.GetConfiguration();
            return new
            {
                success = true,
                category = "audio",
                speakerMode = config.speakerMode.ToString(),
                sampleRate = config.sampleRate,
                dspBufferSize = config.dspBufferSize,
                globalVolume = AudioListener.volume
            };
        }

        private object GetTagsAndLayers()
        {
            return new
            {
                success = true,
                category = "tags_layers",
                tags = UnityEditorInternal.InternalEditorUtility.tags,
                sortingLayers = SortingLayer.layers.Select(l => new { name = l.name, id = l.id, value = l.value }).ToArray(),
                layers = Enumerable.Range(0, 32)
                    .Select(i => new { index = i, name = LayerMask.LayerToName(i) })
                    .Where(l => !string.IsNullOrEmpty(l.name))
                    .ToArray()
            };
        }

        private object GetGraphicsSettings()
        {
            return new
            {
                success = true,
                category = "graphics",
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                renderPipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name ?? "Built-in",
                transparencySortMode = UnityEngine.Rendering.GraphicsSettings.transparencySortMode.ToString()
            };
        }

        private object GetEditorSettings()
        {
            return new
            {
                success = true,
                category = "editor",
                assetSerializationMode = EditorSettings.serializationMode.ToString(),
                defaultBehaviorMode = EditorSettings.defaultBehaviorMode.ToString(),
                externalVersionControl = EditorSettings.externalVersionControl,
                lineEndingsForNewScripts = EditorSettings.lineEndingsForNewScripts.ToString(),
                enterPlayModeSettings = EditorSettings.enterPlayModeOptionsEnabled
            };
        }

        private object SetSettings(Dictionary<string, string> p)
        {
            var category = p.GetValueOrDefault("category") ?? "player";
            var settingsJson = p.GetValueOrDefault("settings");
            if (string.IsNullOrEmpty(settingsJson))
                return new { success = false, error = "settings object is required" };

            var settings = JsonRpcParamsParser.ParseToDictionary(settingsJson);
            var changed = new List<string>();

            switch (category.ToLower())
            {
                case "player":
                    if (settings.TryGetValue("companyName", out var cn)) { PlayerSettings.companyName = cn; changed.Add("companyName"); }
                    if (settings.TryGetValue("productName", out var pn)) { PlayerSettings.productName = pn; changed.Add("productName"); }
                    if (settings.TryGetValue("bundleVersion", out var bv)) { PlayerSettings.bundleVersion = bv; changed.Add("bundleVersion"); }
                    if (settings.TryGetValue("runInBackground", out var rib)) { PlayerSettings.runInBackground = rib.ToLower() == "true"; changed.Add("runInBackground"); }
                    break;

                case "physics":
                    if (settings.TryGetValue("gravityY", out var gy) && float.TryParse(gy, out var gyf))
                    { Physics.gravity = new Vector3(Physics.gravity.x, gyf, Physics.gravity.z); changed.Add("gravity.y"); }
                    break;

                case "time":
                    if (settings.TryGetValue("fixedDeltaTime", out var fdt) && float.TryParse(fdt, out var fdtf))
                    { Time.fixedDeltaTime = fdtf; changed.Add("fixedDeltaTime"); }
                    if (settings.TryGetValue("timeScale", out var ts) && float.TryParse(ts, out var tsf))
                    { Time.timeScale = tsf; changed.Add("timeScale"); }
                    break;

                default:
                    return new { success = false, error = $"Set not implemented for category: {category}. Use execute_code for advanced settings." };
            }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} settings in {category}" };
        }
    }
}
