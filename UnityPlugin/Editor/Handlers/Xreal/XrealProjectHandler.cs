using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL project setup MCP tool requests.
    /// Includes setup_xreal_project, configure_android_build, import_nrsdk, validate_xreal_setup
    /// </summary>
    public class XrealProjectHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "setup_xreal_project",
            "configure_android_build",
            "import_nrsdk",
            "validate_xreal_setup"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "setup_xreal_project":
                    return SetupXrealProject(paramsDict);
                case "configure_android_build":
                    return ConfigureAndroidBuild(paramsDict);
                case "import_nrsdk":
                    return ImportNrsdk(paramsDict);
                case "validate_xreal_setup":
                    return ValidateXrealSetup(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object SetupXrealProject(Dictionary<string, string> @params)
        {
            var projectName = @params.GetValueOrDefault("projectName");
            var companyName = @params.GetValueOrDefault("companyName");
            var packageName = @params.GetValueOrDefault("packageName");
            var enableHandTracking = @params.GetValueOrDefault("enableHandTracking")?.ToLower() != "false";
            var enableImageTracking = @params.GetValueOrDefault("enableImageTracking")?.ToLower() != "false";
            var enablePlaneDetection = @params.GetValueOrDefault("enablePlaneDetection")?.ToLower() != "false";

            var results = new List<string>();
            var errors = new List<string>();

            try
            {
                // 1. Set project name and company
                if (!string.IsNullOrEmpty(projectName))
                {
                    PlayerSettings.productName = projectName;
                    results.Add($"Set product name: {projectName}");
                }

                if (!string.IsNullOrEmpty(companyName))
                {
                    PlayerSettings.companyName = companyName;
                    results.Add($"Set company name: {companyName}");
                }

                // 2. Set bundle identifier
                var bundleId = packageName;
                if (string.IsNullOrEmpty(bundleId))
                {
                    var safeCompany = (companyName ?? "company").ToLower().Replace(" ", "");
                    var safeProduct = (projectName ?? "xrealapp").ToLower().Replace(" ", "");
                    bundleId = $"com.{safeCompany}.{safeProduct}";
                }
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, bundleId);
                results.Add($"Set bundle identifier: {bundleId}");

                // 3. Switch to Android build target
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    results.Add("Switched to Android build target");
                }

                // 4. Configure Android settings
                ConfigureAndroidSettings();
                results.Add("Configured Android build settings for XREAL");

                // 5. Check for XR Plugin Management
                CheckXrPluginManagement(results, errors);

                // 6. Create necessary folders
                EnsureFoldersExist();
                results.Add("Created project folder structure");

                return new
                {
                    success = errors.Count == 0,
                    results = results,
                    errors = errors,
                    nextSteps = new[]
                    {
                        "Import NRSDK using import_nrsdk tool",
                        "Validate setup using validate_xreal_setup tool",
                        "Create XR rig using create_xr_rig tool"
                    }
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    results = results,
                    errors = new[] { ex.Message }
                };
            }
        }

        private void ConfigureAndroidSettings()
        {
            // Set minimum API level (Android 10 / API 29 for XREAL)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel33;

            // Set ARM64 architecture
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Graphics settings
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
#if !UNITY_6000_0_OR_NEWER
            PlayerSettings.MTRendering = true;
            PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, true);
            PlayerSettings.gpuSkinning = true;
#endif

            // Performance settings
            PlayerSettings.graphicsJobs = true;

            // XR settings
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        }

        private void CheckXrPluginManagement(List<string> results, List<string> errors)
        {
#if XR_MANAGEMENT_ENABLED
            results.Add("XR Plugin Management is installed");
#else
            errors.Add("XR Plugin Management not found. Install via Package Manager: com.unity.xr.management");
#endif
        }

        private void EnsureFoldersExist()
        {
            var folders = new[]
            {
                "Assets/Scenes",
                "Assets/Scripts",
                "Assets/Prefabs",
                "Assets/Materials",
                "Assets/StreamingAssets"
            };

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    var parts = folder.Split('/');
                    var current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var next = current + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                        {
                            AssetDatabase.CreateFolder(current, parts[i]);
                        }
                        current = next;
                    }
                }
            }
        }

        private object ConfigureAndroidBuild(Dictionary<string, string> @params)
        {
            var scriptingBackend = @params.GetValueOrDefault("scriptingBackend") ?? "IL2CPP";
            var minSdkStr = @params.GetValueOrDefault("minSdkVersion") ?? "29";
            var targetSdkStr = @params.GetValueOrDefault("targetSdkVersion") ?? "33";
            var multithreadedRendering = @params.GetValueOrDefault("multithreadedRendering")?.ToLower() != "false";
            var gpuSkinning = @params.GetValueOrDefault("gpuSkinning")?.ToLower() != "false";

            try
            {
                // Scripting backend
                var backend = scriptingBackend.ToUpper() == "MONO"
                    ? ScriptingImplementation.Mono2x
                    : ScriptingImplementation.IL2CPP;
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, backend);

                // SDK versions
                if (int.TryParse(minSdkStr, out var minSdk))
                {
                    PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)minSdk;
                }

                if (int.TryParse(targetSdkStr, out var targetSdk))
                {
                    PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)targetSdk;
                }

                // Architecture - always ARM64 for XREAL
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

                // Rendering
#if !UNITY_6000_0_OR_NEWER
                PlayerSettings.SetMobileMTRendering(BuildTargetGroup.Android, multithreadedRendering);
                PlayerSettings.gpuSkinning = gpuSkinning;
#endif

                // Graphics APIs - OpenGL ES 3 for best XREAL compatibility
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });

                // Quality settings for mobile XR
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 72; // XREAL typical refresh rate

                return new
                {
                    success = true,
                    settings = new
                    {
                        scriptingBackend = backend.ToString(),
                        minSdkVersion = minSdk,
                        targetSdkVersion = targetSdk,
                        architecture = "ARM64",
                        multithreadedRendering = multithreadedRendering,
                        gpuSkinning = gpuSkinning,
                        graphicsApi = "OpenGLES3"
                    }
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        private object ImportNrsdk(Dictionary<string, string> @params)
        {
            var source = @params.GetValueOrDefault("source");
            var path = @params.GetValueOrDefault("path");
            var url = @params.GetValueOrDefault("url");
            var importExamples = @params.GetValueOrDefault("importExamples")?.ToLower() == "true";

            if (string.IsNullOrEmpty(source))
            {
                return new { success = false, error = "source is required (local or url)" };
            }

            try
            {
                switch (source.ToLower())
                {
                    case "local":
                        if (string.IsNullOrEmpty(path))
                        {
                            return new { success = false, error = "path is required for local source" };
                        }

                        if (!File.Exists(path))
                        {
                            return new { success = false, error = $"File not found: {path}" };
                        }

                        // Import the unitypackage
                        AssetDatabase.ImportPackage(path, false);

                        return new
                        {
                            success = true,
                            message = $"Imported NRSDK from: {path}",
                            note = "Please wait for import to complete and scripts to recompile"
                        };

                    case "url":
                        return new
                        {
                            success = false,
                            error = "URL download not implemented. Please download NRSDK manually and use 'local' source.",
                            downloadUrl = "https://developer.xreal.com/download"
                        };

                    default:
                        return new { success = false, error = $"Unknown source: {source}" };
                }
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        private object ValidateXrealSetup(Dictionary<string, string> @params)
        {
            var autoFix = @params.GetValueOrDefault("autoFix")?.ToLower() == "true";
            var checkNrsdk = @params.GetValueOrDefault("checkNrsdk")?.ToLower() != "false";
            var checkAndroidSettings = @params.GetValueOrDefault("checkAndroidSettings")?.ToLower() != "false";
            var checkXrPluginManagement = @params.GetValueOrDefault("checkXrPluginManagement")?.ToLower() != "false";
            var checkPermissions = @params.GetValueOrDefault("checkPermissions")?.ToLower() != "false";
            var checkSceneSetup = @params.GetValueOrDefault("checkSceneSetup")?.ToLower() != "false";

            var issues = new List<ValidationIssue>();
            var fixes = new List<string>();

            // Check XREAL SDK presence — accept legacy NRSDK OR the XREAL SDK 3.x UPM package
            // (com.xreal.xr / Unity.XR.XREAL.*). Either path is a valid XREAL setup.
            if (checkNrsdk)
            {
                var hasLegacyNrsdk = XrealSdkDetector.HasLegacyNrsdkFolder()
                                  || XrealSdkDetector.HasLegacyNrsdkType();
                var hasUpmXreal = XrealSdkDetector.HasXrealUpmPackage()
                               || XrealSdkDetector.HasXrealRuntimeType();
                var xrealInstalled = hasLegacyNrsdk || hasUpmXreal;

                if (!xrealInstalled)
                {
                    issues.Add(new ValidationIssue
                    {
                        category = "XREAL SDK",
                        severity = "critical",
                        message = "No XREAL SDK found (looked for Assets/NRSDK, NRKernal.* types, com.xreal.xr UPM package, and Unity.XR.XREAL.* types)",
                        fix = "Install XREAL SDK 3.x via add_package (com.xreal.xr) OR import legacy NRSDK via import_nrsdk"
                    });
                }
            }

            // Check Android settings
            if (checkAndroidSettings)
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    issues.Add(new ValidationIssue
                    {
                        category = "Build Target",
                        severity = "critical",
                        message = "Build target is not Android",
                        fix = "Switch to Android in Build Settings"
                    });

                    if (autoFix)
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                        fixes.Add("Switched to Android build target");
                    }
                }

                if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
                {
                    issues.Add(new ValidationIssue
                    {
                        category = "Android SDK",
                        severity = "warning",
                        message = $"Minimum SDK version ({PlayerSettings.Android.minSdkVersion}) is lower than recommended (API 29)",
                        fix = "Set minimum SDK to API 29"
                    });

                    if (autoFix)
                    {
                        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                        fixes.Add("Set minimum SDK to API 29");
                    }
                }

                if (!PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64))
                {
                    issues.Add(new ValidationIssue
                    {
                        category = "Architecture",
                        severity = "critical",
                        message = "ARM64 architecture not enabled",
                        fix = "Enable ARM64 in Player Settings"
                    });

                    if (autoFix)
                    {
                        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                        fixes.Add("Enabled ARM64 architecture");
                    }
                }

                var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
                if (backend != ScriptingImplementation.IL2CPP)
                {
                    issues.Add(new ValidationIssue
                    {
                        category = "Scripting",
                        severity = "warning",
                        message = "Scripting backend is not IL2CPP (recommended for release)",
                        fix = "Change to IL2CPP in Player Settings"
                    });

                    if (autoFix)
                    {
                        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                        fixes.Add("Changed scripting backend to IL2CPP");
                    }
                }
            }

            // Check XR Plugin Management
            if (checkXrPluginManagement)
            {
#if !XR_MANAGEMENT_ENABLED
                issues.Add(new ValidationIssue
                {
                    category = "XR",
                    severity = "warning",
                    message = "XR Plugin Management not installed",
                    fix = "Install com.unity.xr.management via Package Manager"
                });
#endif
            }

            var isValid = !issues.Any(i => i.severity == "critical");

            return new
            {
                success = true,
                isValid = isValid,
                issueCount = issues.Count,
                criticalCount = issues.Count(i => i.severity == "critical"),
                warningCount = issues.Count(i => i.severity == "warning"),
                issues = issues,
                fixes = fixes.Count > 0 ? fixes : null
            };
        }

        [Serializable]
        private class ValidationIssue
        {
            public string category;
            public string severity;
            public string message;
            public string fix;
        }
    }
}
