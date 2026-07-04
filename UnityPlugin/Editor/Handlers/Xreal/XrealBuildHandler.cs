using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityMcp.Editor.Core;
using Debug = UnityEngine.Debug;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL build and deployment MCP tool requests.
    /// Includes build_xreal_apk, get_connected_devices
    /// </summary>
    public class XrealBuildHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "build_xreal_apk",
            "get_connected_devices",
            "get_build_status"
        };

        // Status entries for asynchronous APK builds. Keyed by jobId. Lives for the lifetime
        // of the editor domain — sufficient for the typical "kick off, poll" workflow without
        // needing to persist across reloads.
        private enum JobState { Queued, Building, Succeeded, Failed }

        private class BuildJob
        {
            public string jobId;
            public JobState state;
            public string outputPath;
            public string buildType;
            public bool developmentBuild;
            public bool runAfterBuild;
            public bool buildAppBundle;
            public string[] scenes;
            public BuildOptions options;
            public DateTime queuedAt;
            public DateTime? startedAt;
            public DateTime? finishedAt;
            public double? buildSeconds;
            public double? fileSizeMb;
            public int totalErrors;
            public int totalWarnings;
            public string error;
            public List<string> errorMessages;
            public object deployment;
        }

        private static readonly Dictionary<string, BuildJob> _jobs = new Dictionary<string, BuildJob>();
        private static readonly object _jobsLock = new object();

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "build_xreal_apk":
                    return BuildXrealApk(paramsDict);
                case "get_connected_devices":
                    return GetConnectedDevices(paramsDict);
                case "get_build_status":
                    return GetBuildStatus(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object BuildXrealApk(Dictionary<string, string> @params)
        {
            var outputPath = @params.GetValueOrDefault("outputPath");
            var buildType = @params.GetValueOrDefault("buildType") ?? "Development";
            var developmentBuild = @params.GetValueOrDefault("developmentBuild")?.ToLower() != "false";
            var scriptDebugging = @params.GetValueOrDefault("scriptDebugging")?.ToLower() == "true";
            var deepProfilingSupport = @params.GetValueOrDefault("deepProfilingSupport")?.ToLower() == "true";
            var autoconnectProfiler = @params.GetValueOrDefault("autoconnectProfiler")?.ToLower() == "true";
            var compressionMethod = @params.GetValueOrDefault("compressionMethod") ?? "LZ4";
            var buildAppBundle = @params.GetValueOrDefault("buildAppBundle")?.ToLower() == "true";
            var runAfterBuild = @params.GetValueOrDefault("runAfterBuild")?.ToLower() == "true";

            // Ensure build target is Android
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                return new { success = false, error = "Build target must be Android. Use configure_android_build first." };
            }

            // Generate output path if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                var buildsDir = "Builds";
                if (!Directory.Exists(buildsDir))
                {
                    Directory.CreateDirectory(buildsDir);
                }

                var extension = buildAppBundle ? ".aab" : ".apk";
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                outputPath = $"{buildsDir}/{PlayerSettings.productName}_{timestamp}{extension}";
            }

            // Get scenes to build
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                return new { success = false, error = "No scenes in build settings. Add scenes via File > Build Settings." };
            }

            // Configure build options
            var buildOptions = BuildOptions.None;

            if (developmentBuild || buildType.ToLower() == "development")
            {
                buildOptions |= BuildOptions.Development;
            }

            if (scriptDebugging)
            {
                buildOptions |= BuildOptions.AllowDebugging;
            }

            if (deepProfilingSupport)
            {
                buildOptions |= BuildOptions.EnableDeepProfilingSupport;
            }

            if (autoconnectProfiler)
            {
                buildOptions |= BuildOptions.ConnectWithProfiler;
            }

            // Configure compression
            switch (compressionMethod.ToUpper())
            {
                case "LZ4":
                    EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
                    break;
                case "LZ4HC":
                    EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
                    break;
            }

            // Set app bundle mode
            EditorUserBuildSettings.buildAppBundle = buildAppBundle;

            // Set up a job entry and schedule the build asynchronously. BuildPipeline.BuildPlayer
            // is itself synchronous and blocks the Editor thread, but by deferring it to
            // EditorApplication.delayCall we return the jobId to the MCP caller immediately
            // (well within the 10s default request timeout), and the caller can poll
            // get_build_status to follow progress and pick up the final BuildReport summary.
            var jobId = Guid.NewGuid().ToString("N");
            var job = new BuildJob
            {
                jobId = jobId,
                state = JobState.Queued,
                outputPath = outputPath,
                buildType = buildType,
                developmentBuild = developmentBuild,
                runAfterBuild = runAfterBuild,
                buildAppBundle = buildAppBundle,
                scenes = scenes,
                options = buildOptions,
                queuedAt = DateTime.Now
            };
            lock (_jobsLock) { _jobs[jobId] = job; }

            EditorApplication.delayCall += () => RunBuildJob(job);

            Debug.Log($"[MCP] Queued XREAL APK build (jobId={jobId}) -> {outputPath}");

            return new
            {
                success = true,
                async = true,
                jobId = jobId,
                state = job.state.ToString(),
                outputPath = outputPath,
                sceneCount = scenes.Length,
                buildType = buildType,
                developmentBuild = developmentBuild,
                buildAppBundle = buildAppBundle,
                message = "Build queued. Poll get_build_status with this jobId for progress."
            };
        }

        private static void RunBuildJob(BuildJob job)
        {
            try
            {
                lock (_jobsLock) { job.state = JobState.Building; }
                job.startedAt = DateTime.Now;
                Debug.Log($"[MCP] Starting XREAL APK build (jobId={job.jobId})");

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = job.scenes,
                    locationPathName = job.outputPath,
                    target = BuildTarget.Android,
                    options = job.options
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                job.finishedAt = DateTime.Now;
                job.buildSeconds = Math.Round((job.finishedAt.Value - job.startedAt.Value).TotalSeconds, 2);
                job.totalErrors = report.summary.totalErrors;
                job.totalWarnings = report.summary.totalWarnings;

                if (report.summary.result == BuildResult.Succeeded)
                {
                    var fileInfo = new FileInfo(job.outputPath);
                    job.fileSizeMb = fileInfo.Exists ? Math.Round(fileInfo.Length / (1024.0 * 1024.0), 2) : 0;
                    lock (_jobsLock) { job.state = JobState.Succeeded; }
                    Debug.Log($"[MCP] Build {job.jobId} succeeded — {job.fileSizeMb}MB in {job.buildSeconds}s");

                    if (job.runAfterBuild)
                    {
                        try
                        {
                            var handler = new XrealBuildHandler();
                            job.deployment = handler.InstallAndRunApk(job.outputPath);
                        }
                        catch (Exception ex)
                        {
                            job.deployment = new { success = false, error = ex.Message };
                        }
                    }
                }
                else
                {
                    var errors = new List<string>();
                    foreach (var step in report.steps)
                    {
                        foreach (var message in step.messages)
                        {
                            if (message.type == LogType.Error)
                                errors.Add(message.content);
                        }
                    }
                    job.errorMessages = errors.Take(10).ToList();
                    job.error = "Build failed";
                    lock (_jobsLock) { job.state = JobState.Failed; }
                    Debug.LogError($"[MCP] Build {job.jobId} failed with {job.totalErrors} error(s) in {job.buildSeconds}s");
                }
            }
            catch (Exception ex)
            {
                job.finishedAt = DateTime.Now;
                if (job.startedAt.HasValue)
                    job.buildSeconds = Math.Round((job.finishedAt.Value - job.startedAt.Value).TotalSeconds, 2);
                job.error = ex.Message;
                job.errorMessages = new List<string> { ex.ToString() };
                lock (_jobsLock) { job.state = JobState.Failed; }
                Debug.LogError($"[MCP] Build {job.jobId} threw: {ex}");
            }
        }

        private object GetBuildStatus(Dictionary<string, string> @params)
        {
            var jobId = @params.GetValueOrDefault("jobId");
            if (string.IsNullOrEmpty(jobId))
            {
                lock (_jobsLock)
                {
                    return new
                    {
                        success = true,
                        message = "No jobId provided — returning all known jobs",
                        jobs = _jobs.Values.Select(SerializeJob).ToList()
                    };
                }
            }

            BuildJob job;
            lock (_jobsLock)
            {
                if (!_jobs.TryGetValue(jobId, out job))
                {
                    return new { success = false, error = $"No build job found with id: {jobId}" };
                }
            }

            return SerializeJob(job);
        }

        private static object SerializeJob(BuildJob job)
        {
            return new
            {
                success = true,
                jobId = job.jobId,
                state = job.state.ToString(),
                isComplete = job.state == JobState.Succeeded || job.state == JobState.Failed,
                outputPath = job.outputPath,
                buildType = job.buildType,
                developmentBuild = job.developmentBuild,
                buildAppBundle = job.buildAppBundle,
                sceneCount = job.scenes?.Length ?? 0,
                queuedAt = job.queuedAt.ToString("o"),
                startedAt = job.startedAt?.ToString("o"),
                finishedAt = job.finishedAt?.ToString("o"),
                buildSeconds = job.buildSeconds,
                fileSizeMb = job.fileSizeMb,
                totalErrors = job.totalErrors,
                totalWarnings = job.totalWarnings,
                error = job.error,
                errors = job.errorMessages,
                deployment = job.deployment
            };
        }

        private object InstallAndRunApk(string apkPath)
        {
            try
            {
                // Get first connected device
                var devices = GetAdbDevices();
                if (devices.Count == 0)
                {
                    return new { success = false, error = "No devices connected" };
                }

                var device = devices[0];
                var adbPath = GetAdbPath();

                // Install APK
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = $"-s {device.serial} install -r \"{apkPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                installProcess.Start();
                var installOutput = installProcess.StandardOutput.ReadToEnd();
                installProcess.WaitForExit(60000);

                if (installProcess.ExitCode != 0)
                {
                    return new { success = false, error = "Install failed", output = installOutput };
                }

                // Launch the app
                var packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
                var launchProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = $"-s {device.serial} shell am start -n {packageName}/com.unity3d.player.UnityPlayerActivity",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                launchProcess.Start();
                launchProcess.WaitForExit(10000);

                return new
                {
                    success = true,
                    device = device.model,
                    packageName = packageName,
                    message = "APK installed and launched"
                };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        private object GetConnectedDevices(Dictionary<string, string> @params)
        {
            var includeEmulators = @params.GetValueOrDefault("includeEmulators")?.ToLower() == "true";
            var includeUnauthorized = @params.GetValueOrDefault("includeUnauthorized")?.ToLower() != "false";
            var checkXrealSupport = @params.GetValueOrDefault("checkXrealSupport")?.ToLower() != "false";
            var refreshDevices = @params.GetValueOrDefault("refreshDevices")?.ToLower() != "false";

            var devices = GetAdbDevices();

            // Filter emulators
            if (!includeEmulators)
            {
                devices = devices.Where(d => !d.serial.StartsWith("emulator-")).ToList();
            }

            // Filter unauthorized
            if (!includeUnauthorized)
            {
                devices = devices.Where(d => d.status == "device").ToList();
            }

            // Check XREAL support
            if (checkXrealSupport)
            {
                foreach (var device in devices)
                {
                    device.xrealSupported = CheckXrealSupport(device.serial);
                }
            }

            return new
            {
                success = true,
                deviceCount = devices.Count,
                devices = devices.Select(d => new
                {
                    d.serial,
                    d.model,
                    d.status,
                    d.androidVersion,
                    xrealSupported = checkXrealSupport ? (bool?)d.xrealSupported : null
                }).ToList()
            };
        }

        private List<DeviceInfo> GetAdbDevices()
        {
            var devices = new List<DeviceInfo>();

            try
            {
                var adbPath = GetAdbPath();
                if (string.IsNullOrEmpty(adbPath))
                {
                    Debug.LogWarning("[MCP] ADB not found in Android SDK");
                    return devices;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = "devices -l",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1)) // Skip "List of devices attached"
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var device = new DeviceInfo
                        {
                            serial = parts[0],
                            status = parts[1]
                        };

                        // Parse additional info
                        for (int i = 2; i < parts.Length; i++)
                        {
                            if (parts[i].StartsWith("model:"))
                            {
                                device.model = parts[i].Substring(6);
                            }
                            else if (parts[i].StartsWith("device:"))
                            {
                                device.deviceName = parts[i].Substring(7);
                            }
                        }

                        // Get Android version
                        if (device.status == "device")
                        {
                            device.androidVersion = GetAndroidVersion(adbPath, device.serial);
                        }

                        devices.Add(device);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Error getting devices: {ex.Message}");
            }

            return devices;
        }

        private string GetAdbPath()
        {
            var sdkPath = EditorPrefs.GetString("AndroidSdkRoot");
            if (string.IsNullOrEmpty(sdkPath))
            {
                sdkPath = Environment.GetEnvironmentVariable("ANDROID_HOME") ??
                          Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
            }

            if (!string.IsNullOrEmpty(sdkPath))
            {
                var adbPath = Path.Combine(sdkPath, "platform-tools", "adb.exe");
                if (File.Exists(adbPath))
                {
                    return adbPath;
                }

                adbPath = Path.Combine(sdkPath, "platform-tools", "adb");
                if (File.Exists(adbPath))
                {
                    return adbPath;
                }
            }

            return null;
        }

        private string GetAndroidVersion(string adbPath, string serial)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = $"-s {serial} shell getprop ro.build.version.release",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                return output;
            }
            catch
            {
                return "Unknown";
            }
        }

        private bool CheckXrealSupport(string serial)
        {
            // Check if device is compatible with XREAL glasses
            // This could check for USB-C DP Alt mode support, specific device models, etc.
            // For now, return true for common Samsung devices
            var supportedModels = new[] { "SM-S921", "SM-S926", "SM-S928", "SM-S911", "SM-S916", "SM-S918" }; // S24, S23 series

            try
            {
                var adbPath = GetAdbPath();
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath,
                        Arguments = $"-s {serial} shell getprop ro.product.model",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var model = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                return supportedModels.Any(m => model.StartsWith(m));
            }
            catch
            {
                return false;
            }
        }

        private class DeviceInfo
        {
            public string serial;
            public string status;
            public string model;
            public string deviceName;
            public string androidVersion;
            public bool xrealSupported;
        }
    }
}
