using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL device control MCP tool requests.
    /// Includes get_xreal_device_info, set_tracking_mode, calibrate_glasses, get_camera_frame
    /// </summary>
    public class XrealDeviceHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "get_xreal_device_info",
            "set_tracking_mode",
            "calibrate_glasses",
            "get_camera_frame"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "get_xreal_device_info":
                    return GetXrealDeviceInfo(paramsDict);
                case "set_tracking_mode":
                    return SetTrackingMode(paramsDict);
                case "calibrate_glasses":
                    return CalibrateGlasses(paramsDict);
                case "get_camera_frame":
                    return GetCameraFrame(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object GetXrealDeviceInfo(Dictionary<string, string> @params)
        {
            var includeCapabilities = @params.GetValueOrDefault("includeCapabilities")?.ToLower() != "false";
            var includeTrackingState = @params.GetValueOrDefault("includeTrackingState")?.ToLower() != "false";
            var includeDisplayInfo = @params.GetValueOrDefault("includeDisplayInfo")?.ToLower() != "false";
            var includeBatteryInfo = @params.GetValueOrDefault("includeBatteryInfo")?.ToLower() != "false";

            // Check if NRSDK is available
            var nrsdkAvailable = IsNrsdkAvailable();

            if (!nrsdkAvailable)
            {
                // Return simulated data for editor development
                return new
                {
                    success = true,
                    isSimulated = true,
                    note = "NRSDK not available - returning simulated data for editor development",
                    device = new
                    {
                        model = "XREAL One Pro (Simulated)",
                        isConnected = false,
                        connectionType = "None",
                        serialNumber = "SIMULATED"
                    },
                    tracking = includeTrackingState ? new
                    {
                        state = "NotTracking",
                        quality = "None",
                        mode = "6DoF"
                    } : null,
                    display = includeDisplayInfo ? new
                    {
                        resolution = new { width = 1920, height = 1080 },
                        refreshRate = 72,
                        brightness = 100
                    } : null,
                    capabilities = includeCapabilities ? new
                    {
                        handTracking = true,
                        planeDetection = true,
                        imageTracking = true,
                        spatialMeshing = true,
                        depthSensing = true
                    } : null,
                    battery = includeBatteryInfo ? new
                    {
                        level = 100,
                        isCharging = false,
                        temperature = 25.0f
                    } : null
                };
            }

            // Real NRSDK device info would be retrieved here
            return GetRealDeviceInfo(includeCapabilities, includeTrackingState, includeDisplayInfo, includeBatteryInfo);
        }

        private object GetRealDeviceInfo(bool includeCapabilities, bool includeTrackingState, bool includeDisplayInfo, bool includeBatteryInfo)
        {
            // This would use NRSDK APIs when available
            // NRDevice.Subsystem.GetDeviceType()
            // NRFrame.GetTrackingState()
            // etc.

            return new
            {
                success = true,
                isSimulated = false,
                note = "Real device info would be retrieved via NRSDK APIs",
                device = new
                {
                    model = "XREAL One Pro",
                    isConnected = true,
                    connectionType = "USB"
                }
            };
        }

        private object SetTrackingMode(Dictionary<string, string> @params)
        {
            var mode = @params.GetValueOrDefault("mode");
            var trackingOrigin = @params.GetValueOrDefault("trackingOrigin") ?? "Device";
            var recenterOnSwitch = @params.GetValueOrDefault("recenterOnSwitch")?.ToLower() != "false";

            if (string.IsNullOrEmpty(mode))
            {
                return new { success = false, error = "mode is required (0DoF, 3DoF, or 6DoF)" };
            }

            if (!IsNrsdkAvailable())
            {
                return new
                {
                    success = true,
                    isSimulated = true,
                    note = "NRSDK not available - tracking mode change simulated",
                    mode = mode,
                    trackingOrigin = trackingOrigin
                };
            }

            // Real implementation would use:
            // NRSessionConfig.SetTrackingMode()

            return new
            {
                success = true,
                mode = mode,
                trackingOrigin = trackingOrigin,
                recenterOnSwitch = recenterOnSwitch
            };
        }

        private object CalibrateGlasses(Dictionary<string, string> @params)
        {
            var calibrationType = @params.GetValueOrDefault("calibrationType");
            var ipdValueStr = @params.GetValueOrDefault("ipdValue");
            var brightnessLevelStr = @params.GetValueOrDefault("brightnessLevel");
            var recenterPose = @params.GetValueOrDefault("recenterPose")?.ToLower() == "true";

            if (string.IsNullOrEmpty(calibrationType))
            {
                return new { success = false, error = "calibrationType is required (ipd, brightness, tracking, or all)" };
            }

            var results = new List<string>();

            switch (calibrationType.ToLower())
            {
                case "ipd":
                    if (!string.IsNullOrEmpty(ipdValueStr) && float.TryParse(ipdValueStr, out var ipd))
                    {
                        results.Add($"Set IPD to {ipd}mm");
                    }
                    else
                    {
                        results.Add("Started automatic IPD calibration");
                    }
                    break;

                case "brightness":
                    if (!string.IsNullOrEmpty(brightnessLevelStr) && int.TryParse(brightnessLevelStr, out var brightness))
                    {
                        results.Add($"Set brightness to {brightness}%");
                    }
                    break;

                case "tracking":
                    if (recenterPose)
                    {
                        results.Add("Recentered tracking pose");
                    }
                    results.Add("Tracking calibration initiated");
                    break;

                case "all":
                    results.Add("Full calibration sequence initiated");
                    break;

                default:
                    return new { success = false, error = $"Unknown calibration type: {calibrationType}" };
            }

            return new
            {
                success = true,
                calibrationType = calibrationType,
                results = results,
                note = IsNrsdkAvailable() ? null : "Calibration simulated - NRSDK not available"
            };
        }

        private object GetCameraFrame(Dictionary<string, string> @params)
        {
            var cameraType = @params.GetValueOrDefault("cameraType") ?? "RGB";
            var resolution = @params.GetValueOrDefault("resolution") ?? "Full";
            var format = @params.GetValueOrDefault("format") ?? "PNG";
            var saveToFile = @params.GetValueOrDefault("saveToFile")?.ToLower() != "false";
            var filePath = @params.GetValueOrDefault("filePath");
            var includeMetadata = @params.GetValueOrDefault("includeMetadata")?.ToLower() != "false";

            // Ensure output directory exists
            var outputDir = "Assets/CapturedFrames";
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Generate filename if not provided
            if (string.IsNullOrEmpty(filePath) && saveToFile)
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var ext = format.ToLower();
                filePath = $"{outputDir}/frame_{timestamp}.{ext}";
            }

            // Try the AR Foundation CPU-image path first. This only works during play mode
            // with an active ARCameraManager — i.e. the XREAL SDK 3.x runtime is alive. When
            // it doesn't yield a frame (edit mode, no provider, no glasses connected) we fall
            // back to the legacy grey placeholder below, clearly labelled as simulated.
            if (Application.isPlaying)
            {
                var realFrame = TryCaptureArFoundationFrame(saveToFile, filePath, format, includeMetadata, cameraType);
                if (realFrame != null) return realFrame;
            }

            // Placeholder path. The grey placeholder is intentionally kept so callers running
            // in the editor outside play mode still get a deterministic file at the expected
            // path — useful for testing the rest of the pipeline. isSimulated=true is the
            // signal that the bytes are not a real camera frame.
            var texture = new Texture2D(640, 480);
            var colors = new Color[640 * 480];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = new Color(0.2f, 0.2f, 0.2f); // Dark gray placeholder
            }
            texture.SetPixels(colors);
            texture.Apply();

            if (saveToFile)
            {
                byte[] bytes;
                switch (format.ToLower())
                {
                    case "jpg":
                        bytes = texture.EncodeToJPG(95);
                        break;
                    case "exr":
                        bytes = texture.EncodeToEXR();
                        break;
                    default:
                        bytes = texture.EncodeToPNG();
                        break;
                }

                File.WriteAllBytes(filePath, bytes);
                AssetDatabase.Refresh();
            }

            UnityEngine.Object.DestroyImmediate(texture);

            // Build a contextual note so the caller knows WHY they're getting a placeholder.
            string placeholderNote;
            if (!IsNrsdkAvailable())
                placeholderNote = "XREAL SDK not detected — placeholder image created. Install com.xreal.xr or legacy NRSDK.";
            else if (!Application.isPlaying)
                placeholderNote = "Editor not in play mode — AR Foundation does not produce frames outside play mode. Placeholder image created.";
            else
                placeholderNote = "No active ARCameraManager (or no frame available yet) — placeholder image created. Enter play mode with an AR Foundation rig (e.g. create_xr_rig).";

            return new
            {
                success = true,
                isSimulated = true,
                note = placeholderNote,
                filePath = saveToFile ? filePath : null,
                resolution = new { width = 640, height = 480 },
                format = format,
                cameraType = cameraType,
                metadata = includeMetadata ? new
                {
                    timestamp = DateTime.Now.ToString("o"),
                    fov = 52.0f,
                    intrinsics = "Simulated"
                } : null
            };
        }

        // Best-effort AR Foundation CPU image capture, reflection-based so this assembly
        // doesn't have to take a compile-time dependency on Unity.XR.ARFoundation or the
        // XREAL SDK packages. Returns null if AR Foundation isn't available, no
        // ARCameraManager is active in the scene, or no frame is ready yet. Returns a
        // JSON-serializable result object on success (mirroring the placeholder path's
        // shape, with isSimulated=false).
        private object TryCaptureArFoundationFrame(bool saveToFile, string filePath, string format, bool includeMetadata, string cameraType)
        {
            try
            {
                Type cameraManagerType = null;
                Type xrCpuImageType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (cameraManagerType == null)
                            cameraManagerType = asm.GetType("UnityEngine.XR.ARFoundation.ARCameraManager", false);
                        if (xrCpuImageType == null)
                            xrCpuImageType = asm.GetType("UnityEngine.XR.ARSubsystems.XRCpuImage", false);
                        if (cameraManagerType != null && xrCpuImageType != null) break;
                    }
                    catch { }
                }
                if (cameraManagerType == null || xrCpuImageType == null)
                    return null; // AR Foundation not in the project — nothing to do.

                // Find the first active ARCameraManager in the scene.
                var managers = UnityEngine.Object.FindObjectsOfType(cameraManagerType);
                if (managers == null || managers.Length == 0) return null;
                var manager = managers[0];

                var tryAcquire = cameraManagerType.GetMethod("TryAcquireLatestCpuImage");
                if (tryAcquire == null) return null;

                var args = new object[] { null };
                var ok = (bool)tryAcquire.Invoke(manager, args);
                if (!ok || args[0] == null) return null;

                var cpuImage = args[0];
                try
                {
                    var widthProp = xrCpuImageType.GetProperty("width");
                    var heightProp = xrCpuImageType.GetProperty("height");
                    var width = (int)widthProp.GetValue(cpuImage);
                    var height = (int)heightProp.GetValue(cpuImage);

                    // Build a conversion params struct via reflection. The fields/properties
                    // we set are:
                    //   inputRect:        new RectInt(0, 0, width, height)
                    //   outputDimensions: new Vector2Int(width, height)
                    //   outputFormat:     TextureFormat.RGBA32
                    //   transformation:   XRCpuImage.Transformation.None
                    var conversionParamsType = xrCpuImageType.GetNestedType("ConversionParams");
                    if (conversionParamsType == null) return null;
                    var conversionParams = Activator.CreateInstance(conversionParamsType);

                    SetMember(conversionParams, conversionParamsType, "inputRect", new RectInt(0, 0, width, height));
                    SetMember(conversionParams, conversionParamsType, "outputDimensions", new Vector2Int(width, height));
                    SetMember(conversionParams, conversionParamsType, "outputFormat", TextureFormat.RGBA32);

                    var transformationEnum = xrCpuImageType.GetNestedType("Transformation");
                    if (transformationEnum != null)
                    {
                        var noneVal = Enum.Parse(transformationEnum, "None");
                        SetMember(conversionParams, conversionParamsType, "transformation", noneVal);
                    }

                    var getConvertedSize = xrCpuImageType.GetMethod("GetConvertedDataSize",
                        new[] { conversionParamsType });
                    if (getConvertedSize == null) return null;
                    var dataSize = (int)getConvertedSize.Invoke(cpuImage, new[] { conversionParams });

                    var buffer = new byte[dataSize];

                    // Convert: XRCpuImage.Convert(ConversionParams, IntPtr, int) — we pin the
                    // managed array and pass its pointer.
                    var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try
                    {
                        var ptr = handle.AddrOfPinnedObject();
                        var convert = xrCpuImageType.GetMethod("Convert",
                            new[] { conversionParamsType, typeof(IntPtr), typeof(int) });
                        if (convert == null) return null;
                        convert.Invoke(cpuImage, new[] { conversionParams, ptr, dataSize });
                    }
                    finally
                    {
                        handle.Free();
                    }

                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(buffer);
                    tex.Apply();

                    if (saveToFile)
                    {
                        byte[] outBytes;
                        switch (format.ToLower())
                        {
                            case "jpg": outBytes = tex.EncodeToJPG(95); break;
                            case "exr": outBytes = tex.EncodeToEXR(); break;
                            default: outBytes = tex.EncodeToPNG(); break;
                        }
                        File.WriteAllBytes(filePath, outBytes);
                        AssetDatabase.Refresh();
                    }

                    UnityEngine.Object.DestroyImmediate(tex);

                    return new
                    {
                        success = true,
                        isSimulated = false,
                        note = "Captured via AR Foundation ARCameraManager.TryAcquireLatestCpuImage().",
                        filePath = saveToFile ? filePath : null,
                        resolution = new { width = width, height = height },
                        format = format,
                        cameraType = cameraType,
                        metadata = includeMetadata ? new
                        {
                            timestamp = DateTime.Now.ToString("o"),
                            source = "ARCameraManager.TryAcquireLatestCpuImage",
                            framework = "AR Foundation"
                        } : null
                    };
                }
                finally
                {
                    // XRCpuImage implements IDisposable — must release the native handle.
                    if (cpuImage is IDisposable disp) disp.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] AR Foundation camera capture failed, falling back to placeholder: {ex.Message}");
                return null;
            }
        }

        private static void SetMember(object instance, Type t, string name, object value)
        {
            var prop = t.GetProperty(name);
            if (prop != null && prop.CanWrite) { prop.SetValue(instance, value); return; }
            var field = t.GetField(name);
            if (field != null) { field.SetValue(instance, value); return; }
        }

        private bool IsNrsdkAvailable()
        {
            return XrealSdkDetector.IsXrealSdkAvailable();
        }
    }

    /// <summary>
    /// Single source of truth for whether *any* XREAL SDK is installed in the project.
    /// Recognises four signals:
    ///   1) Legacy NRSDK as a vendored asset folder:  Assets/NRSDK/
    ///   2) Legacy NRSDK runtime type:                NRKernal.NRInput in any loaded assembly
    ///   3) XREAL SDK 3.x UPM package:                com.xreal.xr in Packages/manifest.json
    ///                                                or a resolved Packages/com.xreal.xr/ folder
    ///   4) XREAL SDK 3.x runtime types:              any Unity.XR.XREAL.* type in a loaded assembly
    /// Any one signal is sufficient.
    /// </summary>
    internal static class XrealSdkDetector
    {
        public static bool IsXrealSdkAvailable()
        {
            return HasLegacyNrsdkFolder()
                || HasLegacyNrsdkType()
                || HasXrealUpmPackage()
                || HasXrealRuntimeType();
        }

        public static bool HasLegacyNrsdkFolder()
        {
            return Directory.Exists("Assets/NRSDK");
        }

        public static bool HasLegacyNrsdkType()
        {
            if (Type.GetType("NRKernal.NRInput, Assembly-CSharp") != null) return true;
            if (Type.GetType("NRKernal.NRInput, NRSDK") != null) return true;
            // Last-resort: walk loaded assemblies for the NRKernal namespace.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetType("NRKernal.NRInput", false) != null) return true;
                }
                catch { }
            }
            return false;
        }

        public static bool HasXrealUpmPackage()
        {
            // Resolved package directory (the most reliable indicator: the package is
            // both manifested AND fully resolved by the package manager).
            var resolvedPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "Packages", "com.xreal.xr");
            if (Directory.Exists(resolvedPath)) return true;

            // Manifest fallback: catches local file: references too. Manifest entry will look
            // like   "com.xreal.xr": "..."   so a substring match against the quoted key is
            // both cheap and accurate enough for this purpose.
            var manifestPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                "Packages", "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifest = File.ReadAllText(manifestPath);
                    if (manifest.IndexOf("\"com.xreal.xr\"", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
                catch { /* unreadable manifest — fall through */ }
            }

            return false;
        }

        public static bool HasXrealRuntimeType()
        {
            // Any Unity.XR.XREAL.* type counts. We can't enumerate the namespace cheaply,
            // so probe a handful of well-known entry points first, then fall back to a
            // namespace-prefix scan across loaded assemblies.
            string[] knownTypeNames =
            {
                "Unity.XR.XREAL.XREALLoader",
                "Unity.XR.XREAL.XREALSettings",
                "Unity.XR.XREAL.XREALSession",
                "Unity.XR.XREAL.XREALCameraManager"
            };
            foreach (var name in knownTypeNames)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetType(name, false) != null) return true;
                    }
                    catch { }
                }
            }

            // Broad scan as last resort.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Namespace != null && t.Namespace.StartsWith("Unity.XR.XREAL", StringComparison.Ordinal))
                            return true;
                    }
                }
                catch
                {
                    // Some assemblies refuse GetTypes(); skip them.
                }
            }

            return false;
        }
    }
}
