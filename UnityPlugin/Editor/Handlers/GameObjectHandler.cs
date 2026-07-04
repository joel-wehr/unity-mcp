using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles GameObject-related MCP tool requests.
    /// </summary>
    public class GameObjectHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "get_gameobject",
            "update_gameobject",
            "delete_gameobject",
            "duplicate_gameobject",
            "find_gameobjects",
            "select_gameobject",
            "editor_selection"
        };

        public object Handle(string method, string paramsJson)
        {
            var dict = ParseParams(paramsJson);

            switch (method)
            {
                case "get_gameobject":
                    return GetGameObject(dict);
                case "update_gameobject":
                    return UpdateGameObject(dict);
                case "delete_gameobject":
                    return DeleteGameObject(dict);
                case "duplicate_gameobject":
                    return DuplicateGameObject(dict);
                case "find_gameobjects":
                    return FindGameObjects(dict);
                case "select_gameobject":
                    return SelectGameObject(dict);
                case "editor_selection":
                    return HandleEditorSelection(dict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private Dictionary<string, object> ParseParams(string paramsJson)
        {
            // Convert string dict to object dict for compatibility with existing methods
            var stringDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var dict = new Dictionary<string, object>();
            foreach (var kvp in stringDict)
            {
                dict[kvp.Key] = kvp.Value;
            }
            return dict;
        }

        private object GetGameObject(Dictionary<string, object> @params)
        {
            var idOrName = @params.GetValueOrDefault("idOrName")?.ToString();
            if (string.IsNullOrEmpty(idOrName))
            {
                return new { success = false, error = "idOrName is required" };
            }

            GameObject go = null;

            // Try to parse as instance ID
            if (int.TryParse(idOrName, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }

            // Try to find by path
            if (go == null)
            {
                go = GameObject.Find(idOrName);
            }

            // Try to find by name in all objects
            if (go == null)
            {
                var allObjects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
                go = allObjects.FirstOrDefault(o => o.name == idOrName);
            }

            if (go == null)
            {
                return new { success = false, error = $"GameObject not found: {idOrName}" };
            }

            return new
            {
                success = true,
                gameObject = SerializeGameObject(go)
            };
        }

        private object UpdateGameObject(Dictionary<string, object> @params)
        {
            var objectPath = @params.GetValueOrDefault("objectPath")?.ToString();
            var instanceIdStr = @params.GetValueOrDefault("instanceId")?.ToString();

            GameObject go = null;

            // Find or create GameObject
            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                go = GameObject.Find(objectPath);
                if (go == null)
                {
                    // Create new GameObject at path
                    go = CreateGameObjectAtPath(objectPath);
                }
            }

            if (go == null)
            {
                return new { success = false, error = "GameObject not found and could not be created" };
            }

            // Extract properties from gameObjectData nested object
            var dataJson = @params.GetValueOrDefault("gameObjectData")?.ToString();
            Dictionary<string, string> data;
            if (!string.IsNullOrEmpty(dataJson) && dataJson.TrimStart().StartsWith("{"))
            {
                data = Core.JsonRpcParamsParser.ParseToDictionary(dataJson);
            }
            else
            {
                // Fallback: look for properties directly in params (backwards compat)
                data = new Dictionary<string, string>();
                foreach (var kvp in @params)
                {
                    if (kvp.Key != "objectPath" && kvp.Key != "instanceId" && kvp.Key != "gameObjectData")
                        data[kvp.Key] = kvp.Value?.ToString();
                }
            }

            Undo.RecordObject(go, "MCP Update GameObject");

            if (data.TryGetValue("name", out var name))
            {
                go.name = name;
            }

            if (data.TryGetValue("tag", out var tag))
            {
                go.tag = tag;
            }

            if (data.TryGetValue("layer", out var layer) && int.TryParse(layer, out var layerInt))
            {
                go.layer = layerInt;
            }

            if (data.TryGetValue("activeSelf", out var active))
            {
                go.SetActive(active.ToLower() == "true");
            }

            if (data.TryGetValue("isStatic", out var isStatic))
            {
                go.isStatic = isStatic.ToLower() == "true";
            }

            EditorUtility.SetDirty(go);
            EditorSceneManager.SaveScene(go.scene);

            return new
            {
                success = true,
                gameObject = SerializeGameObject(go)
            };
        }

        private GameObject CreateGameObjectAtPath(string path)
        {
            var parts = path.Split('/');
            GameObject current = null;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                GameObject child = null;
                if (current == null)
                {
                    child = GameObject.Find(part);
                }
                else
                {
                    var transform = current.transform.Find(part);
                    child = transform?.gameObject;
                }

                if (child == null)
                {
                    child = new GameObject(part);
                    if (current != null)
                    {
                        child.transform.SetParent(current.transform);
                    }
                    Undo.RegisterCreatedObjectUndo(child, "MCP Create GameObject");
                }

                current = child;
            }

            return current;
        }

        private object DeleteGameObject(Dictionary<string, object> @params)
        {
            var objectPath = @params.GetValueOrDefault("objectPath")?.ToString();
            var objectName = @params.GetValueOrDefault("objectName")?.ToString();
            var instanceIdStr = @params.GetValueOrDefault("instanceId")?.ToString();

            GameObject go = null;

            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                go = GameObject.Find(objectPath);
            }
            else if (!string.IsNullOrEmpty(objectName))
            {
                go = GameObject.Find(objectName);
            }

            if (go == null)
            {
                return new { success = false, error = "GameObject not found" };
            }

            var deletedName = go.name;
            var scene = go.scene;
            Undo.DestroyObjectImmediate(go);
            EditorSceneManager.SaveScene(scene);

            return new { success = true, message = $"Deleted GameObject: {deletedName}" };
        }

        private object DuplicateGameObject(Dictionary<string, object> @params)
        {
            var objectPath = @params.GetValueOrDefault("objectPath")?.ToString();
            var objectName = @params.GetValueOrDefault("objectName")?.ToString();
            var instanceIdStr = @params.GetValueOrDefault("instanceId")?.ToString();
            var newName = @params.GetValueOrDefault("newName")?.ToString();

            GameObject go = null;

            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                go = GameObject.Find(objectPath);
            }
            else if (!string.IsNullOrEmpty(objectName))
            {
                go = GameObject.Find(objectName);
            }

            if (go == null)
            {
                return new { success = false, error = "GameObject not found" };
            }

            var duplicate = UnityEngine.Object.Instantiate(go);
            duplicate.name = newName ?? $"{go.name} (Clone)";
            duplicate.transform.SetParent(go.transform.parent);

            Undo.RegisterCreatedObjectUndo(duplicate, "MCP Duplicate GameObject");
            EditorSceneManager.SaveScene(duplicate.scene);

            return new
            {
                success = true,
                gameObject = SerializeGameObject(duplicate)
            };
        }

        private object FindGameObjects(Dictionary<string, object> @params)
        {
            var namePattern = @params.GetValueOrDefault("namePattern")?.ToString();
            var tag = @params.GetValueOrDefault("tag")?.ToString();
            var layerStr = @params.GetValueOrDefault("layer")?.ToString();
            var componentType = @params.GetValueOrDefault("componentType")?.ToString();
            var includeInactiveStr = @params.GetValueOrDefault("includeInactive")?.ToString() ?? "true";
            var maxResultsStr = @params.GetValueOrDefault("maxResults")?.ToString() ?? "100";

            var includeInactive = includeInactiveStr.ToLower() != "false";
            var maxResults = int.TryParse(maxResultsStr, out var mr) ? mr : 100;

            var allObjects = includeInactive
                ? UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>()
                : UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            var results = allObjects.Where(go =>
            {
                // Skip prefabs
                if (!go.scene.IsValid()) return false;

                // Filter by name pattern
                if (!string.IsNullOrEmpty(namePattern))
                {
                    if (namePattern.Contains("*"))
                    {
                        var pattern = namePattern.Replace("*", ".*");
                        if (!System.Text.RegularExpressions.Regex.IsMatch(go.name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            return false;
                    }
                    else if (!go.name.Contains(namePattern))
                    {
                        return false;
                    }
                }

                // Filter by tag
                if (!string.IsNullOrEmpty(tag) && !go.CompareTag(tag))
                    return false;

                // Filter by layer
                if (!string.IsNullOrEmpty(layerStr) && int.TryParse(layerStr, out var layer))
                {
                    if (go.layer != layer) return false;
                }

                // Filter by component type
                if (!string.IsNullOrEmpty(componentType))
                {
                    var type = GetTypeByName(componentType);
                    if (type == null || go.GetComponent(type) == null)
                        return false;
                }

                return true;
            })
            .Take(maxResults)
            .Select(go => new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                path = GetGameObjectPath(go),
                tag = go.tag,
                layer = go.layer,
                activeSelf = go.activeSelf
            })
            .ToList();

            return new
            {
                success = true,
                count = results.Count,
                gameObjects = results
            };
        }

        private object SelectGameObject(Dictionary<string, object> @params)
        {
            var objectPath = @params.GetValueOrDefault("objectPath")?.ToString();
            var objectName = @params.GetValueOrDefault("objectName")?.ToString();
            var instanceIdStr = @params.GetValueOrDefault("instanceId")?.ToString();

            GameObject go = null;

            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                go = GameObject.Find(objectPath);
            }
            else if (!string.IsNullOrEmpty(objectName))
            {
                go = GameObject.Find(objectName);
            }

            if (go == null)
            {
                return new { success = false, error = "GameObject not found" };
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return new
            {
                success = true,
                message = $"Selected: {go.name}",
                instanceId = go.GetInstanceID()
            };
        }

        private object HandleEditorSelection(Dictionary<string, object> @params)
        {
            var action = @params.GetValueOrDefault("action")?.ToString() ?? "get";

            switch (action.ToLower())
            {
                case "get":
                    return new
                    {
                        success = true,
                        selection = Selection.gameObjects.Select(go => new
                        {
                            instanceId = go.GetInstanceID(),
                            name = go.name,
                            path = GetGameObjectPath(go)
                        }).ToList()
                    };

                case "clear":
                    Selection.activeGameObject = null;
                    return new { success = true, message = "Selection cleared" };

                default:
                    return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object SerializeGameObject(GameObject go)
        {
            return new
            {
                instanceId = go.GetInstanceID(),
                name = go.name,
                path = GetGameObjectPath(go),
                tag = go.tag,
                layer = go.layer,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                isStatic = go.isStatic,
                transform = new
                {
                    position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    rotation = new { x = go.transform.rotation.eulerAngles.x, y = go.transform.rotation.eulerAngles.y, z = go.transform.rotation.eulerAngles.z },
                    scale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z }
                },
                components = go.GetComponents<Component>().Select(c => new
                {
                    type = c.GetType().Name,
                    enabled = (c is Behaviour b) ? b.enabled : true
                }).ToList()
            };
        }

        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private Type GetTypeByName(string typeName)
        {
            // Try common Unity types first
            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
            if (type != null) return type;

            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null) return type;

            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName) ?? assembly.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            return null;
        }
    }
}
