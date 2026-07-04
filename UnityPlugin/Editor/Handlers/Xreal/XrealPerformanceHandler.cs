using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Profiling;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL performance and debugging MCP tool requests.
    /// Includes get_xr_performance_metrics, profile_xr_scene, capture_xr_screenshot
    /// </summary>
    public class XrealPerformanceHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "get_xr_performance_metrics",
            "profile_xr_scene",
            "capture_xr_screenshot"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "get_xr_performance_metrics":
                    return GetXrPerformanceMetrics(paramsDict);
                case "profile_xr_scene":
                    return ProfileXrScene(paramsDict);
                case "capture_xr_screenshot":
                    return CaptureXrScreenshot(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object GetXrPerformanceMetrics(Dictionary<string, string> @params)
        {
            var includeFrameMetrics = @params.GetValueOrDefault("includeFrameMetrics")?.ToLower() != "false";
            var includeGpuMetrics = @params.GetValueOrDefault("includeGpuMetrics")?.ToLower() != "false";
            var includeCpuMetrics = @params.GetValueOrDefault("includeCpuMetrics")?.ToLower() != "false";
            var includeMemoryMetrics = @params.GetValueOrDefault("includeMemoryMetrics")?.ToLower() != "false";
            var includeThermalState = @params.GetValueOrDefault("includeThermalState")?.ToLower() != "false";
            var includeTrackingMetrics = @params.GetValueOrDefault("includeTrackingMetrics")?.ToLower() != "false";
            var averageOverFramesStr = @params.GetValueOrDefault("averageOverFrames") ?? "30";

            var averageOverFrames = int.TryParse(averageOverFramesStr, out var aof) ? aof : 30;

            var metrics = new Dictionary<string, object>();

            if (includeFrameMetrics)
            {
                // Note: Accurate frame metrics require play mode
                var targetFps = Application.targetFrameRate > 0 ? Application.targetFrameRate : 72;
                metrics["frame"] = new
                {
                    targetFps = targetFps,
                    currentFps = EditorApplication.isPlaying ? 1.0f / Time.smoothDeltaTime : 0f,
                    frameTime = EditorApplication.isPlaying ? Time.smoothDeltaTime * 1000 : 0f,
                    frameTimeUnit = "ms",
                    droppedFrames = 0,
                    note = EditorApplication.isPlaying ? null : "Frame metrics require play mode"
                };
            }

            if (includeGpuMetrics)
            {
                metrics["gpu"] = new
                {
                    utilization = "N/A (requires device)",
                    renderTime = "N/A",
                    note = "GPU metrics require runtime profiling on device"
                };
            }

            if (includeCpuMetrics)
            {
                metrics["cpu"] = new
                {
                    mainThreadTime = "N/A",
                    renderThreadTime = "N/A",
                    note = "CPU metrics require runtime profiling"
                };
            }

            if (includeMemoryMetrics)
            {
                var totalMemory = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
                var usedMemory = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                var monoMemory = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);
                var gfxMemory = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024 * 1024);

                metrics["memory"] = new
                {
                    totalReserved = totalMemory,
                    totalAllocated = usedMemory,
                    monoHeap = monoMemory,
                    graphicsDriver = gfxMemory,
                    unit = "MB"
                };
            }

            if (includeThermalState)
            {
                metrics["thermal"] = new
                {
                    state = "Unknown",
                    note = "Thermal state requires device connection"
                };
            }

            if (includeTrackingMetrics)
            {
                metrics["tracking"] = new
                {
                    state = "NotTracking",
                    quality = "None",
                    mode = "6DoF",
                    note = "Tracking metrics require NRSDK at runtime"
                };
            }

            return new
            {
                success = true,
                isPlayMode = EditorApplication.isPlaying,
                metrics = metrics,
                averageOverFrames = averageOverFrames,
                note = "Full XR performance metrics require running on device with NRSDK"
            };
        }

        private object ProfileXrScene(Dictionary<string, string> @params)
        {
            var analyzeRendering = @params.GetValueOrDefault("analyzeRendering")?.ToLower() != "false";
            var analyzeMemory = @params.GetValueOrDefault("analyzeMemory")?.ToLower() != "false";
            var analyzePhysics = @params.GetValueOrDefault("analyzePhysics")?.ToLower() != "false";
            var analyzeScripts = @params.GetValueOrDefault("analyzeScripts")?.ToLower() != "false";
            var analyzeAssets = @params.GetValueOrDefault("analyzeAssets")?.ToLower() != "false";
            var generateReport = @params.GetValueOrDefault("generateReport")?.ToLower() != "false";
            var highlightIssues = @params.GetValueOrDefault("highlightIssues")?.ToLower() != "false";
            var targetFrameRateStr = @params.GetValueOrDefault("targetFrameRate") ?? "72";

            var targetFrameRate = float.TryParse(targetFrameRateStr, out var tfr) ? tfr : 72f;
            var targetFrameTime = 1000f / targetFrameRate; // ms

            var issues = new List<ProfileIssue>();
            var stats = new Dictionary<string, object>();

            if (analyzeRendering)
            {
                var renderingStats = AnalyzeRendering(issues, targetFrameTime);
                stats["rendering"] = renderingStats;
            }

            if (analyzeMemory)
            {
                var memoryStats = AnalyzeMemory(issues);
                stats["memory"] = memoryStats;
            }

            if (analyzePhysics)
            {
                var physicsStats = AnalyzePhysics(issues);
                stats["physics"] = physicsStats;
            }

            if (analyzeAssets)
            {
                var assetStats = AnalyzeAssets(issues);
                stats["assets"] = assetStats;
            }

            // Categorize issues by severity
            var criticalCount = issues.Count(i => i.severity == "critical");
            var warningCount = issues.Count(i => i.severity == "warning");
            var infoCount = issues.Count(i => i.severity == "info");

            return new
            {
                success = true,
                targetFrameRate = targetFrameRate,
                targetFrameTime = targetFrameTime,
                stats = stats,
                issueCount = issues.Count,
                criticalCount = criticalCount,
                warningCount = warningCount,
                infoCount = infoCount,
                issues = issues,
                recommendations = GenerateRecommendations(issues)
            };
        }

        private object AnalyzeRendering(List<ProfileIssue> issues, float targetFrameTime)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            var skinnedMeshes = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);

            var totalTriangles = 0L;
            var totalVertices = 0L;
            var materialCount = 0;
            var uniqueMaterials = new HashSet<Material>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    totalTriangles += mf.sharedMesh.triangles.Length / 3;
                    totalVertices += mf.sharedMesh.vertexCount;
                }
            }

            foreach (var smr in skinnedMeshes)
            {
                if (smr.sharedMesh != null)
                {
                    totalTriangles += smr.sharedMesh.triangles.Length / 3;
                    totalVertices += smr.sharedMesh.vertexCount;
                }
            }

            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != null)
                    {
                        uniqueMaterials.Add(mat);
                    }
                }
            }

            materialCount = uniqueMaterials.Count;

            // XR performance thresholds
            if (totalTriangles > 500000)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Rendering",
                    severity = "critical",
                    message = $"High triangle count: {totalTriangles:N0}. Mobile XR should target under 500K.",
                    recommendation = "Reduce mesh complexity, use LOD groups, or cull distant objects"
                });
            }
            else if (totalTriangles > 300000)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Rendering",
                    severity = "warning",
                    message = $"Moderate triangle count: {totalTriangles:N0}. Consider optimization.",
                    recommendation = "Add LOD groups for distant objects"
                });
            }

            var realtimeLights = lights.Count(l => l.type != LightType.Directional && l.lightmapBakeType == LightmapBakeType.Realtime);
            if (realtimeLights > 4)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Rendering",
                    severity = "critical",
                    message = $"Too many realtime lights: {realtimeLights}. Mobile XR should use max 4.",
                    recommendation = "Bake lighting or use fewer dynamic lights"
                });
            }

            if (materialCount > 50)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Rendering",
                    severity = "warning",
                    message = $"High material count: {materialCount}. Consider using texture atlases.",
                    recommendation = "Combine materials using texture atlases to reduce draw calls"
                });
            }

            return new
            {
                renderers = renderers.Length,
                triangles = totalTriangles,
                vertices = totalVertices,
                materials = materialCount,
                lights = lights.Length,
                realtimeLights = realtimeLights
            };
        }

        private object AnalyzeMemory(List<ProfileIssue> issues)
        {
            var textures = UnityEngine.Resources.FindObjectsOfTypeAll<Texture2D>();
            var totalTextureMemory = 0L;
            var largeTextures = new List<string>();

            foreach (var tex in textures)
            {
                var memorySize = Profiler.GetRuntimeMemorySizeLong(tex);
                totalTextureMemory += memorySize;

                if (memorySize > 4 * 1024 * 1024) // > 4MB
                {
                    largeTextures.Add($"{tex.name} ({memorySize / (1024 * 1024)}MB)");
                }
            }

            var totalMemoryMb = totalTextureMemory / (1024 * 1024);

            if (totalMemoryMb > 512)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Memory",
                    severity = "critical",
                    message = $"High texture memory: {totalMemoryMb}MB. Mobile XR should target under 512MB.",
                    recommendation = "Compress textures, reduce resolution, or use mipmaps"
                });
            }

            if (largeTextures.Count > 0)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Memory",
                    severity = "warning",
                    message = $"Found {largeTextures.Count} large textures (>4MB each)",
                    recommendation = "Reduce texture sizes or use compression"
                });
            }

            return new
            {
                textureCount = textures.Length,
                textureMemoryMb = totalMemoryMb,
                largeTextures = largeTextures.Take(5).ToList()
            };
        }

        private object AnalyzePhysics(List<ProfileIssue> issues)
        {
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            var meshColliders = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);

            var convexMeshColliders = meshColliders.Count(mc => mc.convex);
            var nonConvexMeshColliders = meshColliders.Length - convexMeshColliders;

            if (nonConvexMeshColliders > 10)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Physics",
                    severity = "warning",
                    message = $"Found {nonConvexMeshColliders} non-convex MeshColliders",
                    recommendation = "Use convex colliders or primitive colliders for better performance"
                });
            }

            if (rigidbodies.Length > 100)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Physics",
                    severity = "warning",
                    message = $"High Rigidbody count: {rigidbodies.Length}",
                    recommendation = "Consider object pooling or reducing active physics objects"
                });
            }

            return new
            {
                colliders = colliders.Length,
                rigidbodies = rigidbodies.Length,
                meshColliders = meshColliders.Length,
                convexMeshColliders = convexMeshColliders
            };
        }

        private object AnalyzeAssets(List<ProfileIssue> issues)
        {
            var meshes = UnityEngine.Resources.FindObjectsOfTypeAll<Mesh>();
            var audioClips = UnityEngine.Resources.FindObjectsOfTypeAll<AudioClip>();

            var uncompressedAudio = audioClips.Count(a => !a.loadInBackground);

            if (uncompressedAudio > 10)
            {
                issues.Add(new ProfileIssue
                {
                    category = "Assets",
                    severity = "info",
                    message = $"Found {uncompressedAudio} audio clips not set to load in background",
                    recommendation = "Enable 'Load In Background' for large audio files"
                });
            }

            return new
            {
                meshCount = meshes.Length,
                audioClipCount = audioClips.Length,
                uncompressedAudio = uncompressedAudio
            };
        }

        private List<string> GenerateRecommendations(List<ProfileIssue> issues)
        {
            var recommendations = new List<string>();

            if (issues.Any(i => i.category == "Rendering" && i.severity == "critical"))
            {
                recommendations.Add("Implement Level of Detail (LOD) for complex meshes");
                recommendations.Add("Use occlusion culling to hide objects not visible to camera");
            }

            if (issues.Any(i => i.category == "Memory"))
            {
                recommendations.Add("Enable texture compression (ASTC for Android)");
                recommendations.Add("Use power-of-two texture sizes for better compression");
            }

            if (issues.Any(i => i.category == "Physics"))
            {
                recommendations.Add("Use simple collider shapes instead of MeshColliders");
                recommendations.Add("Set appropriate Physics timestep in Project Settings");
            }

            recommendations.Add("Profile on actual XREAL device for accurate metrics");
            recommendations.Add("Use Unity Frame Debugger to identify rendering bottlenecks");

            return recommendations;
        }

        private object CaptureXrScreenshot(Dictionary<string, string> @params)
        {
            var captureMode = @params.GetValueOrDefault("captureMode") ?? "Mono";
            var resolution = @params.GetValueOrDefault("resolution") ?? "Native";
            var format = @params.GetValueOrDefault("format") ?? "PNG";
            var fileName = @params.GetValueOrDefault("fileName");
            var outputPath = @params.GetValueOrDefault("outputPath") ?? "Assets/Screenshots";
            var includeUI = @params.GetValueOrDefault("includeUI")?.ToLower() != "false";
            var transparentBackground = @params.GetValueOrDefault("transparentBackground")?.ToLower() == "true";
            var superSamplingStr = @params.GetValueOrDefault("superSampling") ?? "1";
            var jpgQualityStr = @params.GetValueOrDefault("jpgQuality") ?? "95";

            var superSampling = int.TryParse(superSamplingStr, out var ss) ? Mathf.Clamp(ss, 1, 4) : 1;
            var jpgQuality = int.TryParse(jpgQualityStr, out var jq) ? Mathf.Clamp(jq, 1, 100) : 95;

            // Ensure output directory exists
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Generate filename
            if (string.IsNullOrEmpty(fileName))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var ext = format.ToLower();
                fileName = $"xr_screenshot_{timestamp}.{ext}";
            }

            var fullPath = Path.Combine(outputPath, fileName);

            // Get camera
            var camera = Camera.main;
            if (camera == null)
            {
                return new { success = false, error = "No main camera found in scene" };
            }

            // Determine resolution
            int width, height;
            switch (resolution.ToLower())
            {
                case "1080p":
                    width = 1920;
                    height = 1080;
                    break;
                case "2k":
                    width = 2560;
                    height = 1440;
                    break;
                case "4k":
                    width = 3840;
                    height = 2160;
                    break;
                default: // Native
                    width = camera.pixelWidth;
                    height = camera.pixelHeight;
                    break;
            }

            // Apply supersampling
            width *= superSampling;
            height *= superSampling;

            // Create render texture
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;

            // Store original settings
            var originalTargetTexture = camera.targetTexture;
            var originalClearFlags = camera.clearFlags;
            var originalBackgroundColor = camera.backgroundColor;

            try
            {
                // Configure camera
                camera.targetTexture = rt;

                if (transparentBackground)
                {
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = Color.clear;
                }

                // Render
                camera.Render();

                // Read pixels
                var previousRT = RenderTexture.active;
                RenderTexture.active = rt;

                var texture = new Texture2D(width, height, transparentBackground ? TextureFormat.ARGB32 : TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                RenderTexture.active = previousRT;

                // Encode
                byte[] bytes;
                switch (format.ToLower())
                {
                    case "jpg":
                        bytes = texture.EncodeToJPG(jpgQuality);
                        break;
                    case "exr":
                        bytes = texture.EncodeToEXR();
                        break;
                    default:
                        bytes = texture.EncodeToPNG();
                        break;
                }

                // Save
                File.WriteAllBytes(fullPath, bytes);
                AssetDatabase.Refresh();

                // Cleanup
                UnityEngine.Object.DestroyImmediate(texture);

                return new
                {
                    success = true,
                    filePath = fullPath,
                    resolution = new { width = width / superSampling, height = height / superSampling },
                    capturedResolution = new { width = width, height = height },
                    format = format,
                    captureMode = captureMode,
                    superSampling = superSampling,
                    fileSize = new FileInfo(fullPath).Length / 1024 + "KB"
                };
            }
            finally
            {
                // Restore camera settings
                camera.targetTexture = originalTargetTexture;
                camera.clearFlags = originalClearFlags;
                camera.backgroundColor = originalBackgroundColor;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        [Serializable]
        private class ProfileIssue
        {
            public string category;
            public string severity;
            public string message;
            public string recommendation;
        }
    }
}
