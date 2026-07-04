using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.AI;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles NavMesh baking, agent configuration, and navigation queries.
    /// </summary>
    public class NavMeshHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "navmesh" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "bake": return Bake();
                case "clear": return Clear();
                case "get_settings": return GetSettings();
                case "set_settings": return SetSettings(p);
                case "get_areas": return GetAreas();
                case "set_area": return SetArea(p);
                case "get_agents": return GetAgents();
                case "add_agent": return AddNavMeshAgent(p);
                case "find_path": return FindPath(p);
                case "sample_position": return SamplePosition(p);
                case "get_obstacles": return GetObstacles();
                case "add_obstacle": return AddObstacle(p);
                case "get_surfaces": return GetNavMeshSurfaces();
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object Bake()
        {
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            var triangulation = NavMesh.CalculateTriangulation();
            return new
            {
                success = true,
                message = "NavMesh baked successfully",
                vertices = triangulation.vertices.Length,
                triangles = triangulation.indices.Length / 3
            };
        }

        private object Clear()
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            return new { success = true, message = "All NavMesh data cleared" };
        }

        private object GetSettings()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            return new
            {
                success = true,
                agentRadius = settings.agentRadius,
                agentHeight = settings.agentHeight,
                agentSlope = settings.agentSlope,
                agentClimb = settings.agentClimb,
                agentTypeID = settings.agentTypeID
            };
        }

        private object SetSettings(Dictionary<string, string> p)
        {
            // NavMesh settings are configured via the serialized NavMeshBuildSettings
            // We use reflection to access the internal NavMeshBuilder settings
            var changed = new List<string>();

            // Use SerializedObject on NavMeshProjectSettings
            var settingsAsset = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .FirstOrDefault(o => o.GetType().Name == "NavMeshProjectSettings");

            if (settingsAsset == null)
            {
                // Fallback: modify via NavMeshBuildSettings
                return new { success = false, error = "NavMesh settings must be configured via Navigation window or NavMeshSurface components" };
            }

            var so = new SerializedObject(settingsAsset);
            var settingsArr = so.FindProperty("m_Settings");
            if (settingsArr != null && settingsArr.isArray && settingsArr.arraySize > 0)
            {
                var first = settingsArr.GetArrayElementAtIndex(0);

                if (p.TryGetValue("agentRadius", out var ar) && float.TryParse(ar, out var arf))
                {
                    var prop = first.FindPropertyRelative("agentRadius");
                    if (prop != null) { prop.floatValue = arf; changed.Add("agentRadius"); }
                }
                if (p.TryGetValue("agentHeight", out var ah) && float.TryParse(ah, out var ahf))
                {
                    var prop = first.FindPropertyRelative("agentHeight");
                    if (prop != null) { prop.floatValue = ahf; changed.Add("agentHeight"); }
                }
                if (p.TryGetValue("agentSlope", out var asl) && float.TryParse(asl, out var aslf))
                {
                    var prop = first.FindPropertyRelative("agentSlope");
                    if (prop != null) { prop.floatValue = aslf; changed.Add("agentSlope"); }
                }
                if (p.TryGetValue("agentClimb", out var ac) && float.TryParse(ac, out var acf))
                {
                    var prop = first.FindPropertyRelative("agentClimb");
                    if (prop != null) { prop.floatValue = acf; changed.Add("agentClimb"); }
                }

                so.ApplyModifiedProperties();
            }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} NavMesh settings" };
        }

        private object GetAreas()
        {
            var areas = new List<object>();
            var areaNames = UnityEditor.GameObjectUtility.GetNavMeshAreaNames();
            for (int i = 0; i < 32; i++)
            {
                var areaName = areaNames.ElementAtOrDefault(i);
                if (!string.IsNullOrEmpty(areaName))
                {
                    var cost = NavMesh.GetAreaCost(i);
                    areas.Add(new { index = i, name = areaName, cost = cost });
                }
            }
            return new { success = true, areas = areas };
        }

        private object SetArea(Dictionary<string, string> p)
        {
            var indexStr = p.GetValueOrDefault("areaIndex");
            var costStr = p.GetValueOrDefault("cost");

            if (string.IsNullOrEmpty(indexStr) || !int.TryParse(indexStr, out var index))
                return new { success = false, error = "areaIndex is required (integer)" };
            if (string.IsNullOrEmpty(costStr) || !float.TryParse(costStr, out var cost))
                return new { success = false, error = "cost is required (float)" };

            NavMesh.SetAreaCost(index, cost);
            return new { success = true, message = $"Set area {index} cost to {cost}" };
        }

        private object GetAgents()
        {
            var agents = UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
            var list = agents.Select(a => new
            {
                name = a.gameObject.name,
                instanceId = McpId.Get(a.gameObject),
                agentTypeID = a.agentTypeID,
                speed = a.speed,
                angularSpeed = a.angularSpeed,
                acceleration = a.acceleration,
                stoppingDistance = a.stoppingDistance,
                radius = a.radius,
                height = a.height,
                isOnNavMesh = a.isOnNavMesh,
                hasPath = a.hasPath,
                pathStatus = a.pathStatus.ToString(),
                remainingDistance = a.remainingDistance
            }).ToList();

            return new { success = true, count = list.Count, agents = list };
        }

        private object AddNavMeshAgent(Dictionary<string, string> p)
        {
            var objectPath = p.GetValueOrDefault("objectPath");
            var objectId = p.GetValueOrDefault("objectId");

            GameObject go = null;
            if (!string.IsNullOrEmpty(objectPath))
                go = GameObject.Find(objectPath);
            if (go == null && !string.IsNullOrEmpty(objectId) && int.TryParse(objectId, out var id))
                go = McpId.ToObject(id) as GameObject;
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                agent = Undo.AddComponent<NavMeshAgent>(go);
            }

            if (p.TryGetValue("speed", out var sp) && float.TryParse(sp, out var spf))
                agent.speed = spf;
            if (p.TryGetValue("angularSpeed", out var asp) && float.TryParse(asp, out var aspf))
                agent.angularSpeed = aspf;
            if (p.TryGetValue("acceleration", out var acc) && float.TryParse(acc, out var accf))
                agent.acceleration = accf;
            if (p.TryGetValue("stoppingDistance", out var sd) && float.TryParse(sd, out var sdf))
                agent.stoppingDistance = sdf;

            EditorUtility.SetDirty(go);
            return new { success = true, message = $"NavMeshAgent configured on '{go.name}'", instanceId = McpId.Get(go) };
        }

        private object FindPath(Dictionary<string, string> p)
        {
            var fromJson = p.GetValueOrDefault("from");
            var toJson = p.GetValueOrDefault("to");

            if (string.IsNullOrEmpty(fromJson) || string.IsNullOrEmpty(toJson))
                return new { success = false, error = "from and to positions required (JSON: {\"x\":0,\"y\":0,\"z\":0})" };

            var fromP = JsonRpcParamsParser.ParseToDictionary(fromJson);
            var toP = JsonRpcParamsParser.ParseToDictionary(toJson);

            float.TryParse(fromP.GetValueOrDefault("x") ?? "0", out var fx);
            float.TryParse(fromP.GetValueOrDefault("y") ?? "0", out var fy);
            float.TryParse(fromP.GetValueOrDefault("z") ?? "0", out var fz);

            float.TryParse(toP.GetValueOrDefault("x") ?? "0", out var tx);
            float.TryParse(toP.GetValueOrDefault("y") ?? "0", out var ty);
            float.TryParse(toP.GetValueOrDefault("z") ?? "0", out var tz);

            var from = new Vector3(fx, fy, fz);
            var to = new Vector3(tx, ty, tz);

            var path = new NavMeshPath();
            var found = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path);

            var corners = path.corners.Select(c => new { x = c.x, y = c.y, z = c.z }).ToList();

            return new
            {
                success = true,
                pathFound = found,
                status = path.status.ToString(),
                cornerCount = corners.Count,
                corners = corners
            };
        }

        private object SamplePosition(Dictionary<string, string> p)
        {
            var xStr = p.GetValueOrDefault("x") ?? "0";
            var yStr = p.GetValueOrDefault("y") ?? "0";
            var zStr = p.GetValueOrDefault("z") ?? "0";
            var rangeStr = p.GetValueOrDefault("range") ?? "10";

            float.TryParse(xStr, out var x);
            float.TryParse(yStr, out var y);
            float.TryParse(zStr, out var z);
            float.TryParse(rangeStr, out var range);

            var found = NavMesh.SamplePosition(new Vector3(x, y, z), out var hit, range, NavMesh.AllAreas);

            return new
            {
                success = true,
                found = found,
                position = found ? new { x = hit.position.x, y = hit.position.y, z = hit.position.z } : null,
                distance = found ? hit.distance : -1f
            };
        }

        private object GetObstacles()
        {
            var obstacles = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsSortMode.None);
            var list = obstacles.Select(o => new
            {
                name = o.gameObject.name,
                instanceId = McpId.Get(o.gameObject),
                shape = o.shape.ToString(),
                carving = o.carving,
                size = new { x = o.size.x, y = o.size.y, z = o.size.z },
                center = new { x = o.center.x, y = o.center.y, z = o.center.z }
            }).ToList();

            return new { success = true, count = list.Count, obstacles = list };
        }

        private object AddObstacle(Dictionary<string, string> p)
        {
            var objectPath = p.GetValueOrDefault("objectPath");
            var objectId = p.GetValueOrDefault("objectId");

            GameObject go = null;
            if (!string.IsNullOrEmpty(objectPath))
                go = GameObject.Find(objectPath);
            if (go == null && !string.IsNullOrEmpty(objectId) && int.TryParse(objectId, out var id))
                go = McpId.ToObject(id) as GameObject;
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = Undo.AddComponent<NavMeshObstacle>(go);
            }

            if (p.TryGetValue("carving", out var carving))
                obstacle.carving = carving.ToLower() == "true";

            if (p.TryGetValue("shape", out var shape))
            {
                switch (shape.ToLower())
                {
                    case "box": obstacle.shape = NavMeshObstacleShape.Box; break;
                    case "capsule": obstacle.shape = NavMeshObstacleShape.Capsule; break;
                }
            }

            EditorUtility.SetDirty(go);
            return new { success = true, message = $"NavMeshObstacle configured on '{go.name}'" };
        }

        private object GetNavMeshSurfaces()
        {
            // NavMeshSurface is in the AI Navigation package — use reflection
            var surfaceType = FindType("NavMeshSurface");
            if (surfaceType == null)
                return new { success = true, count = 0, surfaces = new object[0], note = "NavMeshSurface component not found. Install AI Navigation package." };

            var surfaces = UnityEngine.Object.FindObjectsByType(surfaceType, FindObjectsSortMode.None);
            var list = surfaces.Select(s =>
            {
                var go = (s as Component)?.gameObject;
                return new
                {
                    name = go?.name ?? "unknown",
                    instanceId = McpId.Get(go)
                };
            }).ToList();

            return new { success = true, count = list.Count, surfaces = list };
        }

        private Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
