using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles ScriptableObject creation, inspection, and modification.
    /// </summary>
    public class ScriptableObjectHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "scriptable_object" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "create": return Create(p);
                case "get_properties": return GetProperties(p);
                case "set_property": return SetProperty(p);
                case "list": return ListScriptableObjects(p);
                case "find_by_type": return FindByType(p);
                case "duplicate": return Duplicate(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object Create(Dictionary<string, string> p)
        {
            var typeName = p.GetValueOrDefault("typeName");
            var assetPath = p.GetValueOrDefault("assetPath");

            if (string.IsNullOrEmpty(typeName))
                return new { success = false, error = "typeName is required" };
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            // Find the type across all assemblies
            Type soType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                soType = asm.GetType(typeName);
                if (soType != null) break;
                // Try without namespace
                soType = asm.GetTypes().FirstOrDefault(t => t.Name == typeName && typeof(ScriptableObject).IsAssignableFrom(t));
                if (soType != null) break;
            }

            if (soType == null)
                return new { success = false, error = $"Type '{typeName}' not found" };
            if (!typeof(ScriptableObject).IsAssignableFrom(soType))
                return new { success = false, error = $"Type '{typeName}' is not a ScriptableObject" };

            var instance = ScriptableObject.CreateInstance(soType);
            if (!assetPath.EndsWith(".asset")) assetPath += ".asset";

            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                var parts = dir.Replace("\\", "/").Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();

            return new
            {
                success = true,
                path = assetPath,
                type = soType.FullName,
                message = $"Created {soType.Name} at {assetPath}"
            };
        }

        private object GetProperties(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
                return new { success = false, error = $"ScriptableObject not found at: {assetPath}" };

            var so = new SerializedObject(asset);
            var properties = new List<object>();
            var iterator = so.GetIterator();
            iterator.Next(true); // Enter first child

            while (iterator.NextVisible(false))
            {
                properties.Add(new
                {
                    name = iterator.name,
                    displayName = iterator.displayName,
                    type = iterator.propertyType.ToString(),
                    value = GetSerializedPropertyValue(iterator),
                    isArray = iterator.isArray,
                    depth = iterator.depth
                });
            }

            return new
            {
                success = true,
                path = assetPath,
                type = asset.GetType().FullName,
                properties = properties
            };
        }

        private object SetProperty(Dictionary<string, string> p)
        {
            var assetPath = p.GetValueOrDefault("assetPath");
            var propertyName = p.GetValueOrDefault("propertyName");
            var propertyValue = p.GetValueOrDefault("propertyValue");

            if (string.IsNullOrEmpty(assetPath))
                return new { success = false, error = "assetPath is required" };
            if (string.IsNullOrEmpty(propertyName))
                return new { success = false, error = "propertyName is required" };

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (asset == null)
                return new { success = false, error = $"ScriptableObject not found at: {assetPath}" };

            var so = new SerializedObject(asset);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
                return new { success = false, error = $"Property '{propertyName}' not found" };

            SetSerializedPropertyValue(prop, propertyValue);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new { success = true, message = $"Set {propertyName} = {propertyValue}" };
        }

        private object ListScriptableObjects(Dictionary<string, string> p)
        {
            var folder = p.GetValueOrDefault("folder") ?? "Assets";
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder });

            var assets = guids.Take(100).Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                return new
                {
                    path = path,
                    name = asset?.name ?? "unknown",
                    type = asset?.GetType().Name ?? "unknown"
                };
            }).ToList();

            return new { success = true, count = assets.Count, totalFound = guids.Length, assets = assets };
        }

        private object FindByType(Dictionary<string, string> p)
        {
            var typeName = p.GetValueOrDefault("typeName");
            if (string.IsNullOrEmpty(typeName))
                return new { success = false, error = "typeName is required" };

            var guids = AssetDatabase.FindAssets($"t:{typeName}");
            var assets = guids.Take(50).Select(guid =>
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                return new { path = path, guid = guid };
            }).ToList();

            return new { success = true, typeName = typeName, count = assets.Count, assets = assets };
        }

        private object Duplicate(Dictionary<string, string> p)
        {
            var sourcePath = p.GetValueOrDefault("assetPath");
            var destPath = p.GetValueOrDefault("destPath");

            if (string.IsNullOrEmpty(sourcePath))
                return new { success = false, error = "assetPath is required" };
            if (string.IsNullOrEmpty(destPath))
                return new { success = false, error = "destPath is required" };

            if (!AssetDatabase.CopyAsset(sourcePath, destPath))
                return new { success = false, error = $"Failed to copy {sourcePath} to {destPath}" };

            AssetDatabase.SaveAssets();
            return new { success = true, source = sourcePath, destination = destPath };
        }

        private string GetSerializedPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum: return prop.enumDisplayNames[prop.enumValueIndex];
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue?.name ?? "null";
                case SerializedPropertyType.Vector2:
                    return $"({prop.vector2Value.x:F2}, {prop.vector2Value.y:F2})";
                case SerializedPropertyType.Vector3:
                    return $"({prop.vector3Value.x:F2}, {prop.vector3Value.y:F2}, {prop.vector3Value.z:F2})";
                case SerializedPropertyType.Color:
                    return $"({prop.colorValue.r:F2}, {prop.colorValue.g:F2}, {prop.colorValue.b:F2}, {prop.colorValue.a:F2})";
                default:
                    return prop.propertyType.ToString();
            }
        }

        private void SetSerializedPropertyValue(SerializedProperty prop, string value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, out var iv)) prop.intValue = iv;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value?.ToLower() == "true";
                    break;
                case SerializedPropertyType.Float:
                    if (float.TryParse(value, out var fv)) prop.floatValue = fv;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    break;
                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out var ei)) prop.enumValueIndex = ei;
                    else
                    {
                        var idx = Array.IndexOf(prop.enumDisplayNames, value);
                        if (idx >= 0) prop.enumValueIndex = idx;
                    }
                    break;
            }
        }
    }
}
