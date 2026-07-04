using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles Component-related MCP tool requests.
    /// </summary>
    public class ComponentHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "update_component"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = ParseParams(paramsJson);

            switch (method)
            {
                case "update_component":
                    return UpdateComponent(paramsDict);
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

        private object UpdateComponent(Dictionary<string, object> @params)
        {
            var objectPath = @params.GetValueOrDefault("objectPath")?.ToString();
            var instanceIdStr = @params.GetValueOrDefault("instanceId")?.ToString();
            var componentName = @params.GetValueOrDefault("componentName")?.ToString();

            if (string.IsNullOrEmpty(componentName))
            {
                return new { success = false, error = "componentName is required" };
            }

            // Find GameObject
            GameObject go = null;

            if (!string.IsNullOrEmpty(instanceIdStr) && int.TryParse(instanceIdStr, out var instanceId))
            {
                go = McpId.ToObject(instanceId) as GameObject;
            }
            else if (!string.IsNullOrEmpty(objectPath))
            {
                go = GameObject.Find(objectPath);
            }

            if (go == null)
            {
                return new { success = false, error = "GameObject not found" };
            }

            // Find or get component type
            var componentType = GetTypeByName(componentName);
            if (componentType == null)
            {
                return new { success = false, error = $"Component type not found: {componentName}" };
            }

            // Get or add component
            var component = go.GetComponent(componentType);
            var wasAdded = false;

            if (component == null)
            {
                component = go.AddComponent(componentType);
                wasAdded = true;
                Undo.RegisterCreatedObjectUndo(component, "MCP Add Component");
            }
            else
            {
                Undo.RecordObject(component, "MCP Update Component");
            }

            // Apply component data if provided
            if (@params.TryGetValue("componentData", out var componentDataObj) && componentDataObj != null)
            {
                ApplyComponentData(component, componentDataObj.ToString());
            }

            EditorUtility.SetDirty(component);
            EditorSceneManager.SaveScene(go.scene);

            return new
            {
                success = true,
                wasAdded = wasAdded,
                componentType = componentType.Name,
                gameObjectName = go.name
            };
        }

        private void ApplyComponentData(Component component, string dataJson)
        {
            if (string.IsNullOrEmpty(dataJson) || dataJson == "{}") return;

            var data = Core.JsonRpcParamsParser.ParseToDictionary(dataJson);
            if (data.Count == 0) return;

            // Primary approach: SerializedObject (handles private serialized fields, enums, vectors, etc.)
            var serializedObj = new SerializedObject(component);

            foreach (var kvp in data)
            {
                try
                {
                    // Try the field name as-is, then with m_ prefix (Unity convention)
                    var prop = serializedObj.FindProperty(kvp.Key)
                            ?? serializedObj.FindProperty("m_" + char.ToUpper(kvp.Key[0]) + kvp.Key.Substring(1));

                    if (prop != null)
                    {
                        SetSerializedProperty(prop, kvp.Value);
                        continue;
                    }

                    // Fallback: reflection for public fields/properties
                    SetViaReflection(component, kvp.Key, kvp.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP] Failed to set '{kvp.Key}': {ex.Message}");
                }
            }

            serializedObj.ApplyModifiedProperties();
        }

        private void SetSerializedProperty(SerializedProperty prop, string value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, out var intVal))
                        prop.intValue = intVal;
                    break;

                case SerializedPropertyType.Float:
                    if (float.TryParse(value, out var floatVal))
                        prop.floatValue = floatVal;
                    break;

                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToLower() == "true";
                    break;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var enumInt))
                        prop.enumValueIndex = enumInt;
                    else
                    {
                        // Try matching by name
                        var names = prop.enumNames;
                        for (int i = 0; i < names.Length; i++)
                        {
                            if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                            {
                                prop.enumValueIndex = i;
                                break;
                            }
                        }
                    }
                    break;

                case SerializedPropertyType.Vector2:
                    prop.vector2Value = ParseVector2(value);
                    break;

                case SerializedPropertyType.Vector3:
                    prop.vector3Value = ParseVector3(value);
                    break;

                case SerializedPropertyType.Vector4:
                    prop.vector4Value = ParseVector4(value);
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = ParseColor(value);
                    break;

                case SerializedPropertyType.Rect:
                    prop.rectValue = ParseRect(value);
                    break;

                case SerializedPropertyType.Vector2Int:
                    var v2 = ParseVector2(value);
                    prop.vector2IntValue = new Vector2Int((int)v2.x, (int)v2.y);
                    break;

                case SerializedPropertyType.Vector3Int:
                    var v3 = ParseVector3(value);
                    prop.vector3IntValue = new Vector3Int((int)v3.x, (int)v3.y, (int)v3.z);
                    break;

                default:
                    Debug.LogWarning($"[MCP] Unsupported property type '{prop.propertyType}' for '{prop.name}'");
                    break;
            }
        }

        private void SetViaReflection(Component component, string fieldName, string value)
        {
            var type = component.GetType();

            // Try public field
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var converted = ConvertValue(value, field.FieldType);
                if (converted != null) field.SetValue(component, converted);
                return;
            }

            // Try public property
            var property = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                var converted = ConvertValue(value, property.PropertyType);
                if (converted != null) property.SetValue(component, converted);
            }
        }

        private object ConvertValue(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;

            if (targetType == typeof(int))
                return int.TryParse(value, out var i) ? i : (object)null;

            if (targetType == typeof(float))
                return float.TryParse(value, out var f) ? f : (object)null;

            if (targetType == typeof(double))
                return double.TryParse(value, out var d) ? d : (object)null;

            if (targetType == typeof(bool))
                return value.ToLower() == "true";

            if (targetType.IsEnum)
            {
                if (int.TryParse(value, out var enumInt))
                    return Enum.ToObject(targetType, enumInt);
                if (Enum.TryParse(targetType, value, true, out var enumVal))
                    return enumVal;
                return null;
            }

            if (targetType == typeof(Vector2))
                return ParseVector2(value);

            if (targetType == typeof(Vector3))
                return ParseVector3(value);

            if (targetType == typeof(Vector4))
                return ParseVector4(value);

            if (targetType == typeof(Color))
                return ParseColor(value);

            return null;
        }

        private static Vector2 ParseVector2(string json)
        {
            var d = Core.JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("x", "0"), out var x);
            float.TryParse(d.GetValueOrDefault("y", "0"), out var y);
            return new Vector2(x, y);
        }

        private static Vector3 ParseVector3(string json)
        {
            var d = Core.JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("x", "0"), out var x);
            float.TryParse(d.GetValueOrDefault("y", "0"), out var y);
            float.TryParse(d.GetValueOrDefault("z", "0"), out var z);
            return new Vector3(x, y, z);
        }

        private static Vector4 ParseVector4(string json)
        {
            var d = Core.JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("x", "0"), out var x);
            float.TryParse(d.GetValueOrDefault("y", "0"), out var y);
            float.TryParse(d.GetValueOrDefault("z", "0"), out var z);
            float.TryParse(d.GetValueOrDefault("w", "0"), out var w);
            return new Vector4(x, y, z, w);
        }

        private static Color ParseColor(string json)
        {
            var d = Core.JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("r", "0"), out var r);
            float.TryParse(d.GetValueOrDefault("g", "0"), out var g);
            float.TryParse(d.GetValueOrDefault("b", "0"), out var b);
            float.TryParse(d.GetValueOrDefault("a", "1"), out var a);
            return new Color(r, g, b, a);
        }

        private static Rect ParseRect(string json)
        {
            var d = Core.JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("x", "0"), out var x);
            float.TryParse(d.GetValueOrDefault("y", "0"), out var y);
            float.TryParse(d.GetValueOrDefault("width", "0"), out var w);
            float.TryParse(d.GetValueOrDefault("height", "0"), out var h);
            return new Rect(x, y, w, h);
        }

        private Type GetTypeByName(string typeName)
        {
            // Common namespace prefixes to try
            var prefixes = new[]
            {
                "UnityEngine.",
                "UnityEngine.UI.",
                "UnityEngine.EventSystems.",
                "UnityEngine.Rendering.",
                "TMPro.",
                ""
            };

            // Common assembly names for quick lookup
            var assemblies = new[]
            {
                "UnityEngine",
                "UnityEngine.CoreModule",
                "UnityEngine.UIModule",
                "UnityEngine.PhysicsModule",
                "UnityEngine.Physics2DModule",
                "UnityEngine.AnimationModule",
                "UnityEngine.AudioModule",
                "UnityEngine.ParticleSystemModule",
                "Unity.TextMeshPro",
                "UnityEngine.UI"
            };

            // Try each prefix+assembly combo
            foreach (var prefix in prefixes)
            {
                var fullName = prefix + typeName;
                foreach (var asm in assemblies)
                {
                    var type = Type.GetType($"{fullName}, {asm}");
                    if (type != null) return type;
                }
            }

            // Exhaustive search across all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var prefix in prefixes)
                {
                    var type = assembly.GetType(prefix + typeName);
                    if (type != null) return type;
                }
            }

            return null;
        }
    }
}
