using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.Profiling;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class ProfilerHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "profiler" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "start": return StartProfiler(p);
                case "stop": return StopProfiler();
                case "get_status": return GetStatus();
                case "get_frame_data": return GetFrameData(p);
                case "get_memory_snapshot": return GetMemorySnapshot();
                case "get_render_stats": return GetRenderStats();
                case "get_cpu_usage": return GetCpuUsage();
                case "get_gc_allocs": return GetGcAllocs();
                case "clear": return ClearProfiler();
                case "save_report": return SaveReport(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object StartProfiler(Dictionary<string, string> p)
        {
            var deepProfile = p.GetValueOrDefault("deepProfile")?.ToLower() == "true";

            ProfilerDriver.enabled = true;
            ProfilerDriver.deepProfiling = deepProfile;

            return new
            {
                success = true,
                message = "Profiler started",
                deepProfiling = deepProfile
            };
        }

        private object StopProfiler()
        {
            ProfilerDriver.enabled = false;
            return new { success = true, message = "Profiler stopped" };
        }

        private object GetStatus()
        {
            return new
            {
                success = true,
                enabled = ProfilerDriver.enabled,
                deepProfiling = ProfilerDriver.deepProfiling,
                firstFrameIndex = ProfilerDriver.firstFrameIndex,
                lastFrameIndex = ProfilerDriver.lastFrameIndex
            };
        }

        private object GetFrameData(Dictionary<string, string> p)
        {
            var frameCountStr = p.GetValueOrDefault("frameCount") ?? "1";
            var frameCount = int.TryParse(frameCountStr, out var fc) ? Math.Min(fc, 300) : 1;

            var lastFrame = ProfilerDriver.lastFrameIndex;
            if (lastFrame < 0)
            {
                return new
                {
                    success = true,
                    message = "No profiler data available. Start the profiler and run at least one frame.",
                    firstFrame = ProfilerDriver.firstFrameIndex,
                    lastFrame = lastFrame,
                    frames = new object[0]
                };
            }

            var frames = new List<object>();

            for (int i = 0; i < frameCount && (lastFrame - i) >= ProfilerDriver.firstFrameIndex; i++)
            {
                var frameIndex = lastFrame - i;

                // Get frame timing data via FrameTimingManager
                float cpuMs = 0f, gpuMs = 0f;

                try
                {
                    var timings = new FrameTiming[1];
                    FrameTimingManager.CaptureFrameTimings();
                    var count = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
                    if (count > 0)
                    {
                        cpuMs = (float)timings[0].cpuFrameTime;
                        gpuMs = (float)timings[0].gpuFrameTime;
                    }
                }
                catch { /* FrameTimingManager may not be available in editor-only mode */ }

                frames.Add(new
                {
                    frameIndex = frameIndex,
                    cpuTimeMs = cpuMs,
                    gpuTimeMs = gpuMs,
                    fps = cpuMs > 0 ? 1000f / cpuMs : 0f
                });
            }

            return new
            {
                success = true,
                firstFrame = ProfilerDriver.firstFrameIndex,
                lastFrame = lastFrame,
                requestedFrames = frameCount,
                frames = frames
            };
        }

        private object GetMemorySnapshot()
        {
            var totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            var totalReserved = Profiler.GetTotalReservedMemoryLong();
            var totalUnused = Profiler.GetTotalUnusedReservedMemoryLong();
            var monoHeap = Profiler.GetMonoHeapSizeLong();
            var monoUsed = Profiler.GetMonoUsedSizeLong();
            var tempAllocator = Profiler.GetTempAllocatorSize();
            var gfxAllocated = Profiler.GetAllocatedMemoryForGraphicsDriver();

            return new
            {
                success = true,
                totalAllocatedMB = totalAllocated / (1024.0 * 1024.0),
                totalReservedMB = totalReserved / (1024.0 * 1024.0),
                totalUnusedMB = totalUnused / (1024.0 * 1024.0),
                monoHeapMB = monoHeap / (1024.0 * 1024.0),
                monoUsedMB = monoUsed / (1024.0 * 1024.0),
                tempAllocatorMB = tempAllocator / (1024.0 * 1024.0),
                gfxAllocatedMB = gfxAllocated / (1024.0 * 1024.0)
            };
        }

        private object GetRenderStats()
        {
            // UnityStats is only available in play mode
            if (!EditorApplication.isPlaying)
            {
                return new
                {
                    success = true,
                    message = "Detailed render stats require play mode. Showing available editor data.",
                    isPlayMode = false,
                    sceneViewCount = SceneView.sceneViews.Count,
                    currentQualityLevel = QualitySettings.GetQualityLevel(),
                    qualityLevelName = QualitySettings.names[QualitySettings.GetQualityLevel()],
                    vSyncCount = QualitySettings.vSyncCount,
                    targetFrameRate = Application.targetFrameRate,
                    maxTextureSize = QualitySettings.globalTextureMipmapLimit
                };
            }

            // In play mode, use UnityStats via reflection (internal class)
            try
            {
                var statsType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UnityStats");
                if (statsType != null)
                {
                    var drawCalls = GetStaticField<int>(statsType, "drawCalls");
                    var batches = GetStaticField<int>(statsType, "batches");
                    var tris = GetStaticField<int>(statsType, "triangles");
                    var verts = GetStaticField<int>(statsType, "vertices");
                    var setPasses = GetStaticField<int>(statsType, "setPassCalls");
                    var screenRes = GetStaticField<string>(statsType, "screenRes");
                    var fps = GetStaticField<float>(statsType, "frameTime");

                    return new
                    {
                        success = true,
                        isPlayMode = true,
                        drawCalls = drawCalls,
                        batches = batches,
                        triangles = tris,
                        vertices = verts,
                        setPassCalls = setPasses,
                        screenResolution = screenRes,
                        frameTimeMs = fps
                    };
                }
            }
            catch { /* UnityStats reflection failed */ }

            return new
            {
                success = true,
                message = "Render stats not accessible via UnityStats",
                isPlayMode = true
            };
        }

        private object GetCpuUsage()
        {
            var totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            var monoUsed = Profiler.GetMonoUsedSizeLong();

            // Get timing data
            float cpuMs = 0f;
            try
            {
                var timings = new FrameTiming[1];
                FrameTimingManager.CaptureFrameTimings();
                var count = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
                if (count > 0)
                    cpuMs = (float)timings[0].cpuFrameTime;
            }
            catch { }

            // Get profiler area statistics if profiler is enabled
            var areas = new Dictionary<string, object>();
            if (ProfilerDriver.enabled && ProfilerDriver.lastFrameIndex >= 0)
            {
                var areaNames = new[] { "Rendering", "Scripts", "Physics", "Animation", "GarbageCollector", "VSync", "UI", "Audio" };
                foreach (var areaName in areaNames)
                {
                    try
                    {
                        // Try to parse as ProfilerArea enum
                        if (Enum.TryParse<ProfilerArea>(areaName, out var area))
                        {
                            var timeMs = ProfilerDriver.GetFormattedStatisticsValue(ProfilerDriver.lastFrameIndex, (int)area);
                            areas[areaName] = timeMs;
                        }
                    }
                    catch { }
                }
            }

            return new
            {
                success = true,
                cpuFrameTimeMs = cpuMs,
                fps = cpuMs > 0 ? 1000f / cpuMs : 0f,
                profilerEnabled = ProfilerDriver.enabled,
                managedMemoryMB = monoUsed / (1024.0 * 1024.0),
                areas = areas
            };
        }

        private object GetGcAllocs()
        {
            var monoHeap = Profiler.GetMonoHeapSizeLong();
            var monoUsed = Profiler.GetMonoUsedSizeLong();
            var totalAllocated = Profiler.GetTotalAllocatedMemoryLong();

            return new
            {
                success = true,
                monoHeapMB = monoHeap / (1024.0 * 1024.0),
                monoUsedMB = monoUsed / (1024.0 * 1024.0),
                monoFreeMB = (monoHeap - monoUsed) / (1024.0 * 1024.0),
                totalManagedAllocatedMB = totalAllocated / (1024.0 * 1024.0),
                gcCollectionCount0 = GC.CollectionCount(0),
                gcCollectionCount1 = GC.CollectionCount(1),
                gcCollectionCount2 = GC.CollectionCount(2),
                gcTotalMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                message = "For per-frame GC allocation tracking, enable deep profiling with the 'start' action."
            };
        }

        private object ClearProfiler()
        {
            ProfilerDriver.ClearAllFrames();
            return new { success = true, message = "Profiler data cleared" };
        }

        private object SaveReport(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("savePath");
            if (string.IsNullOrEmpty(path))
            {
                var dir = System.IO.Path.Combine(Application.dataPath, "..", "ProfilerReports");
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                path = System.IO.Path.Combine(dir, $"ProfilerReport_{DateTime.Now:yyyyMMdd_HHmmss}.raw");
            }

            ProfilerDriver.SaveProfile(path);
            return new { success = true, path = path, message = $"Profiler data saved to {path}" };
        }

        private static T GetStaticField<T>(Type type, string fieldName)
        {
            try
            {
                var prop = type.GetProperty(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (prop != null)
                    return (T)prop.GetValue(null);
                var field = type.GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (field != null)
                    return (T)field.GetValue(null);
            }
            catch { }
            return default;
        }
    }
}
