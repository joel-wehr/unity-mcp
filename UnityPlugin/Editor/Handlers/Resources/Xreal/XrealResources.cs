using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Resources.Xreal
{
    /// <summary>
    /// Handles xreal://device_state resource
    /// </summary>
    public class DeviceStateResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://device_state" };

        public object Handle(string uri, string paramsJson)
        {
            var nrsdkInstalled = Directory.Exists("Assets/NRSDK") ||
                                Type.GetType("NRKernal.NRInput, Assembly-CSharp") != null;

            return new
            {
                success = true,
                isSimulated = true,
                nrsdkInstalled = nrsdkInstalled,
                device = new
                {
                    model = "XREAL One Pro (Simulated)",
                    isConnected = false,
                    connectionType = "None"
                },
                tracking = new
                {
                    state = "NotTracking",
                    quality = "None",
                    mode = "6DoF"
                },
                note = "Real device state requires NRSDK at runtime"
            };
        }
    }

    /// <summary>
    /// Handles xreal://hand_tracking/{hand} resource
    /// </summary>
    public class HandTrackingResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://hand_tracking/{hand}", "xreal://hand_tracking" };

        public object Handle(string uri, string paramsJson)
        {
            var parts = uri.Split('/');
            var hand = parts.Length > 2 ? parts.Last() : "Both";

            return new
            {
                success = true,
                isSimulated = true,
                hand = hand,
                enabled = false,
                leftHand = (hand == "Both" || hand == "Left") ? CreateSimulatedHand("Left") : null,
                rightHand = (hand == "Both" || hand == "Right") ? CreateSimulatedHand("Right") : null,
                note = "Real hand tracking requires NRSDK at runtime"
            };
        }

        private object CreateSimulatedHand(string hand)
        {
            return new
            {
                hand = hand,
                isTracked = false,
                confidence = 0f,
                gesture = "None",
                pinchStrength = 0f,
                grabStrength = 0f
            };
        }
    }

    /// <summary>
    /// Handles xreal://spatial_anchors resource
    /// </summary>
    public class SpatialAnchorsResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://spatial_anchors" };

        public object Handle(string uri, string paramsJson)
        {
            var anchors = new List<object>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                if (go.name.StartsWith("SpatialAnchor_"))
                {
                    anchors.Add(new
                    {
                        name = go.name.Replace("SpatialAnchor_", ""),
                        instanceId = go.GetInstanceID(),
                        position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                        rotation = new { x = go.transform.rotation.x, y = go.transform.rotation.y, z = go.transform.rotation.z, w = go.transform.rotation.w }
                    });
                }
            }

            return new
            {
                success = true,
                count = anchors.Count,
                anchors = anchors
            };
        }
    }

    /// <summary>
    /// Handles xreal://detected_planes resource
    /// </summary>
    public class DetectedPlanesResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://detected_planes" };

        public object Handle(string uri, string paramsJson)
        {
            // Return simulated planes for editor development
            var planes = new List<object>
            {
                new
                {
                    id = "plane_floor_001",
                    type = "Horizontal",
                    classification = "Floor",
                    area = 10.5f,
                    position = new { x = 0, y = 0, z = 0 }
                },
                new
                {
                    id = "plane_wall_001",
                    type = "Vertical",
                    classification = "Wall",
                    area = 8.0f,
                    position = new { x = 0, y = 1.5f, z = 2.0f }
                }
            };

            return new
            {
                success = true,
                isSimulated = true,
                count = planes.Count,
                planes = planes,
                note = "Real plane detection requires NRSDK at runtime"
            };
        }
    }

    /// <summary>
    /// Handles xreal://tracked_images resource
    /// </summary>
    public class TrackedImagesResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://tracked_images" };

        public object Handle(string uri, string paramsJson)
        {
            // Check for configured tracking images
            var configDir = "Assets/StreamingAssets/TrackingImages";
            var configuredImages = new List<object>();

            if (Directory.Exists(configDir))
            {
                var configFiles = Directory.GetFiles(configDir, "*_config.json");
                foreach (var file in configFiles)
                {
                    var json = File.ReadAllText(file);
                    configuredImages.Add(new
                    {
                        configFile = Path.GetFileName(file),
                        content = json
                    });
                }
            }

            return new
            {
                success = true,
                isSimulated = true,
                configuredImageCount = configuredImages.Count,
                configuredImages = configuredImages,
                trackedImages = new List<object>(), // Empty at edit time
                note = "Real image tracking requires NRSDK at runtime"
            };
        }
    }

    /// <summary>
    /// Handles xreal://build_settings resource
    /// </summary>
    public class BuildSettingsResource : IResourceHandler
    {
        public string[] SupportedUris => new[] { "xreal://build_settings" };

        public object Handle(string uri, string paramsJson)
        {
            var isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;

            return new
            {
                success = true,
                buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                isAndroid = isAndroid,
                android = isAndroid ? new
                {
                    minSdkVersion = PlayerSettings.Android.minSdkVersion.ToString(),
                    targetSdkVersion = PlayerSettings.Android.targetSdkVersion.ToString(),
                    targetArchitectures = PlayerSettings.Android.targetArchitectures.ToString(),
                    scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android).ToString(),
                    bundleIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)
                } : null,
                player = new
                {
                    productName = PlayerSettings.productName,
                    companyName = PlayerSettings.companyName,
                    version = PlayerSettings.bundleVersion
                },
                graphics = new
                {
                    graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)?.Select(g => g.ToString()).ToArray(),
#if !UNITY_6000_0_OR_NEWER
                    multithreadedRendering = PlayerSettings.MTRendering,
                    gpuSkinning = PlayerSettings.gpuSkinning
#else
                    multithreadedRendering = true,
                    gpuSkinning = true
#endif
                },
                xr = new
                {
#if XR_MANAGEMENT_ENABLED
                    xrManagementInstalled = true,
#else
                    xrManagementInstalled = false,
#endif
                    nrsdkInstalled = Directory.Exists("Assets/NRSDK")
                },
                scenes = EditorBuildSettings.scenes.Select(s => new
                {
                    path = s.path,
                    enabled = s.enabled
                }).ToArray()
            };
        }
    }
}
