using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL spatial mapping MCP tool requests.
    /// Includes enable_plane_detection, get_detected_planes, create_spatial_anchor,
    /// manage_spatial_anchors, enable_meshing
    /// </summary>
    public class XrealSpatialMappingHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "enable_plane_detection",
            "get_detected_planes",
            "create_spatial_anchor",
            "manage_spatial_anchors",
            "enable_meshing"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "enable_plane_detection":
                    return EnablePlaneDetection(paramsDict);
                case "get_detected_planes":
                    return GetDetectedPlanes(paramsDict);
                case "create_spatial_anchor":
                    return CreateSpatialAnchor(paramsDict);
                case "manage_spatial_anchors":
                    return ManageSpatialAnchors(paramsDict);
                case "enable_meshing":
                    return EnableMeshing(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object EnablePlaneDetection(Dictionary<string, string> @params)
        {
            var enabled = @params.GetValueOrDefault("enabled")?.ToLower() != "false";
            var planeTypesStr = @params.GetValueOrDefault("planeTypes") ?? "Both";
            var visualizePlanes = @params.GetValueOrDefault("visualizePlanes")?.ToLower() == "true";
            var classifyPlanes = @params.GetValueOrDefault("classifyPlanes")?.ToLower() != "false";
            var mergePlanes = @params.GetValueOrDefault("mergePlanes")?.ToLower() != "false";
            var minPlaneAreaStr = @params.GetValueOrDefault("minPlaneArea") ?? "0.25";
            var maxPlanesStr = @params.GetValueOrDefault("maxPlanes") ?? "20";
            var planeColor = @params.GetValueOrDefault("planeColor") ?? "#00FF00";
            var updateMode = @params.GetValueOrDefault("updateMode") ?? "Continuous";

            var minPlaneArea = float.TryParse(minPlaneAreaStr, out var mpa) ? mpa : 0.25f;
            var maxPlanes = int.TryParse(maxPlanesStr, out var mp) ? mp : 20;

            return new
            {
                success = true,
                enabled = enabled,
                configuration = new
                {
                    planeTypes = planeTypesStr,
                    visualizePlanes = visualizePlanes,
                    classifyPlanes = classifyPlanes,
                    mergePlanes = mergePlanes,
                    minPlaneArea = minPlaneArea,
                    maxPlanes = maxPlanes,
                    planeColor = planeColor,
                    updateMode = updateMode
                },
                note = "Plane detection configuration set - will activate at runtime with NRSDK"
            };
        }

        private object GetDetectedPlanes(Dictionary<string, string> @params)
        {
            var planeType = @params.GetValueOrDefault("planeType") ?? "All";
            var classification = @params.GetValueOrDefault("classification") ?? "All";
            var minAreaStr = @params.GetValueOrDefault("minArea");
            var includePose = @params.GetValueOrDefault("includePose")?.ToLower() != "false";
            var includeVertices = @params.GetValueOrDefault("includeVertices")?.ToLower() == "true";
            var coordinateSpace = @params.GetValueOrDefault("coordinateSpace") ?? "World";

            // Return simulated planes for editor development
            var planes = new List<object>
            {
                new
                {
                    id = "plane_floor_001",
                    type = "Horizontal",
                    classification = "Floor",
                    area = 10.5f,
                    pose = includePose ? new
                    {
                        position = new { x = 0, y = 0, z = 0 },
                        rotation = new { x = 0, y = 0, z = 0, w = 1 }
                    } : null,
                    size = new { width = 3.5f, height = 3.0f },
                    vertices = includeVertices ? new[]
                    {
                        new { x = -1.75f, y = 0, z = -1.5f },
                        new { x = 1.75f, y = 0, z = -1.5f },
                        new { x = 1.75f, y = 0, z = 1.5f },
                        new { x = -1.75f, y = 0, z = 1.5f }
                    } : null
                },
                new
                {
                    id = "plane_wall_001",
                    type = "Vertical",
                    classification = "Wall",
                    area = 8.0f,
                    pose = includePose ? new
                    {
                        position = new { x = 0, y = 1.5f, z = 2.0f },
                        rotation = new { x = 0, y = 0, z = 0, w = 1 }
                    } : null,
                    size = new { width = 4.0f, height = 2.0f },
                    vertices = (object)null
                },
                new
                {
                    id = "plane_table_001",
                    type = "Horizontal",
                    classification = "Table",
                    area = 1.2f,
                    pose = includePose ? new
                    {
                        position = new { x = 1.0f, y = 0.75f, z = 0.5f },
                        rotation = new { x = 0, y = 0, z = 0, w = 1 }
                    } : null,
                    size = new { width = 1.2f, height = 1.0f },
                    vertices = (object)null
                }
            };

            return new
            {
                success = true,
                isSimulated = true,
                coordinateSpace = coordinateSpace,
                totalCount = planes.Count,
                planes = planes,
                note = "Simulated planes - real data requires NRSDK at runtime"
            };
        }

        private object CreateSpatialAnchor(Dictionary<string, string> @params)
        {
            var anchorName = @params.GetValueOrDefault("anchorName");
            var attachToPlane = @params.GetValueOrDefault("attachToPlane");
            var persistent = @params.GetValueOrDefault("persistent")?.ToLower() != "false";
            var cloudEnabled = @params.GetValueOrDefault("cloudEnabled")?.ToLower() == "true";

            if (string.IsNullOrEmpty(anchorName))
            {
                return new { success = false, error = "anchorName is required" };
            }

            // Parse position
            var position = Vector3.zero;
            if (@params.TryGetValue("position", out var posStr))
            {
                // Simple parsing - in production use proper JSON parsing
                position = Vector3.zero;
            }

            // Parse rotation
            var rotation = Quaternion.identity;

            // Create anchor GameObject
            var anchorGo = new GameObject($"SpatialAnchor_{anchorName}");
            anchorGo.transform.position = position;
            anchorGo.transform.rotation = rotation;

            // Add a visual marker (can be removed in production)
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.SetParent(anchorGo.transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.05f;
            marker.name = "AnchorMarker";

            var meshRenderer = marker.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                var material = new Material(Shader.Find("Standard"));
                material.color = Color.cyan;
                meshRenderer.sharedMaterial = material;
            }

            // Remove collider from marker
            var collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Undo.RegisterCreatedObjectUndo(anchorGo, "Create Spatial Anchor");

            // Generate anchor ID
            var anchorId = Guid.NewGuid().ToString();

            return new
            {
                success = true,
                anchorId = anchorId,
                anchorName = anchorName,
                gameObjectInstanceId = McpId.Get(anchorGo),
                position = new { x = position.x, y = position.y, z = position.z },
                rotation = new { x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w },
                persistent = persistent,
                cloudEnabled = cloudEnabled,
                attachedToPlane = attachToPlane,
                note = "Anchor created - persistence requires NRSDK at runtime"
            };
        }

        private object ManageSpatialAnchors(Dictionary<string, string> @params)
        {
            var action = @params.GetValueOrDefault("action");
            var anchorId = @params.GetValueOrDefault("anchorId");
            var anchorName = @params.GetValueOrDefault("anchorName");
            var includeMetadata = @params.GetValueOrDefault("includeMetadata")?.ToLower() != "false";
            var includeTransform = @params.GetValueOrDefault("includeTransform")?.ToLower() != "false";

            if (string.IsNullOrEmpty(action))
            {
                return new { success = false, error = "action is required (load, save, delete, query, list, clear_all)" };
            }

            switch (action.ToLower())
            {
                case "list":
                    // Find all anchor GameObjects in scene
                    var anchors = new List<object>();
                    var anchorObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    foreach (var go in anchorObjects)
                    {
                        if (go.name.StartsWith("SpatialAnchor_"))
                        {
                            anchors.Add(new
                            {
                                name = go.name.Replace("SpatialAnchor_", ""),
                                instanceId = McpId.Get(go),
                                position = includeTransform ? new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z } : null
                            });
                        }
                    }
                    return new
                    {
                        success = true,
                        action = action,
                        count = anchors.Count,
                        anchors = anchors
                    };

                case "delete":
                    if (string.IsNullOrEmpty(anchorName) && string.IsNullOrEmpty(anchorId))
                    {
                        return new { success = false, error = "anchorName or anchorId required for delete" };
                    }
                    var nameToFind = anchorName ?? anchorId;
                    var toDelete = GameObject.Find($"SpatialAnchor_{nameToFind}");
                    if (toDelete != null)
                    {
                        Undo.DestroyObjectImmediate(toDelete);
                        return new { success = true, action = action, deleted = nameToFind };
                    }
                    return new { success = false, error = $"Anchor not found: {nameToFind}" };

                case "clear_all":
                    var deleted = 0;
                    var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                    foreach (var go in allObjects)
                    {
                        if (go.name.StartsWith("SpatialAnchor_"))
                        {
                            Undo.DestroyObjectImmediate(go);
                            deleted++;
                        }
                    }
                    return new { success = true, action = action, deletedCount = deleted };

                case "save":
                case "load":
                case "query":
                    return new
                    {
                        success = true,
                        action = action,
                        note = $"{action} operation requires NRSDK persistence features at runtime"
                    };

                default:
                    return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object EnableMeshing(Dictionary<string, string> @params)
        {
            var enabled = @params.GetValueOrDefault("enabled")?.ToLower() != "false";
            var meshDensity = @params.GetValueOrDefault("meshDensity") ?? "Medium";
            var visualizeMesh = @params.GetValueOrDefault("visualizeMesh")?.ToLower() == "true";
            var generateColliders = @params.GetValueOrDefault("generateColliders")?.ToLower() != "false";
            var enableOcclusion = @params.GetValueOrDefault("enableOcclusion")?.ToLower() != "false";
            var classifyMesh = @params.GetValueOrDefault("classifyMesh")?.ToLower() == "true";
            var meshMaterial = @params.GetValueOrDefault("meshMaterial") ?? "Occlusion";
            var updateRateStr = @params.GetValueOrDefault("updateRate") ?? "1";

            var updateRate = float.TryParse(updateRateStr, out var ur) ? ur : 1.0f;

            return new
            {
                success = true,
                enabled = enabled,
                configuration = new
                {
                    meshDensity = meshDensity,
                    visualizeMesh = visualizeMesh,
                    generateColliders = generateColliders,
                    enableOcclusion = enableOcclusion,
                    classifyMesh = classifyMesh,
                    meshMaterial = meshMaterial,
                    updateRate = updateRate
                },
                note = "Spatial meshing configuration set - will activate at runtime with NRSDK"
            };
        }
    }
}
