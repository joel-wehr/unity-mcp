using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class DebuggerHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "debugger" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "evaluate_expression": return EvaluateExpression(p);
                case "dump_object": return DumpObject(p);
                case "invoke_method": return InvokeMethod(p);
                case "get_static_field": return GetStaticField(p);
                case "set_static_field": return SetStaticField(p);
                case "call_static_method": return CallStaticMethod(p);
                case "debug_log": return DebugLog(p);
                case "get_component_values": return GetComponentValues(p);
                case "get_stack_trace": return new { success = true, message = "Stack trace only available during breakpoint debugging" };
                case "list_breakpoints": return new { success = true, message = "Breakpoint management requires IDE integration" };
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object EvaluateExpression(Dictionary<string, string> p)
        {
            var expression = p.GetValueOrDefault("expression");
            if (string.IsNullOrEmpty(expression))
                return new { success = false, error = "expression is required" };

            // Handle common expressions
            try
            {
                // Try as a static property/field access: TypeName.Member
                if (expression.Contains('.'))
                {
                    var lastDot = expression.LastIndexOf('.');
                    var typePart = expression.Substring(0, lastDot);
                    var memberPart = expression.Substring(lastDot + 1);

                    var type = FindType(typePart);
                    if (type != null)
                    {
                        // Try property
                        var prop = type.GetProperty(memberPart, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (prop != null)
                        {
                            var val = prop.GetValue(null);
                            return new { success = true, result = val?.ToString(), type = val?.GetType().Name ?? "null" };
                        }
                        // Try field
                        var field = type.GetField(memberPart, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            var val = field.GetValue(null);
                            return new { success = true, result = val?.ToString(), type = val?.GetType().Name ?? "null" };
                        }
                    }
                }

                return new { success = false, error = $"Cannot evaluate expression: {expression}. Use call_static_method or get_static_field for specific operations, or use execute_code for complex expressions." };
            }
            catch (Exception ex)
            {
                return new { success = false, error = ex.Message };
            }
        }

        private object DumpObject(Dictionary<string, string> p)
        {
            var go = FindGameObject(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var maxDepthStr = p.GetValueOrDefault("maxDepth") ?? "3";
            var maxDepth = int.TryParse(maxDepthStr, out var md) ? Math.Min(md, 10) : 3;
            var includePrivate = p.GetValueOrDefault("includePrivate")?.ToLower() == "true";

            var components = go.GetComponents<Component>();
            var dump = new List<object>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                var compType = comp.GetType();
                var flags = BindingFlags.Instance | BindingFlags.Public;
                if (includePrivate) flags |= BindingFlags.NonPublic;

                var fields = compType.GetFields(flags)
                    .Select(f => new { name = f.Name, value = SafeToString(f.GetValue(comp)), type = f.FieldType.Name, isPublic = f.IsPublic })
                    .ToList();

                var props = compType.GetProperties(flags)
                    .Where(pr => pr.CanRead && pr.GetIndexParameters().Length == 0)
                    .Select(pr =>
                    {
                        try { return new { name = pr.Name, value = SafeToString(pr.GetValue(comp)), type = pr.PropertyType.Name }; }
                        catch { return new { name = pr.Name, value = "<error reading>", type = pr.PropertyType.Name }; }
                    })
                    .ToList();

                dump.Add(new
                {
                    component = compType.Name,
                    fullType = compType.FullName,
                    fields = fields,
                    properties = props
                });
            }

            return new
            {
                success = true,
                name = go.name,
                active = go.activeSelf,
                layer = go.layer,
                tag = go.tag,
                components = dump
            };
        }

        private object InvokeMethod(Dictionary<string, string> p)
        {
            var go = FindGameObject(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var componentType = p.GetValueOrDefault("componentType");
            var methodName = p.GetValueOrDefault("methodName");
            if (string.IsNullOrEmpty(methodName))
                return new { success = false, error = "methodName is required" };

            Component comp = null;
            if (!string.IsNullOrEmpty(componentType))
            {
                var type = FindType(componentType);
                if (type != null) comp = go.GetComponent(type);
            }

            if (comp == null)
            {
                // Try all components
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var m = c.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null) { comp = c; break; }
                }
            }

            if (comp == null)
                return new { success = false, error = $"Component with method '{methodName}' not found on {go.name}" };

            var method = comp.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                return new { success = false, error = $"Method '{methodName}' not found" };

            var argsJson = p.GetValueOrDefault("methodArgs");
            object[] args = ParseMethodArgs(argsJson, method.GetParameters());

            var result = method.Invoke(comp, args);
            return new { success = true, result = result?.ToString(), resultType = result?.GetType().Name ?? "void" };
        }

        private object GetStaticField(Dictionary<string, string> p)
        {
            var typeName = p.GetValueOrDefault("typeName");
            var fieldName = p.GetValueOrDefault("fieldName");
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(fieldName))
                return new { success = false, error = "typeName and fieldName are required" };

            var type = FindType(typeName);
            if (type == null) return new { success = false, error = $"Type not found: {typeName}" };

            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return new { success = true, value = SafeToString(field.GetValue(null)), type = field.FieldType.Name };

            var prop = type.GetProperty(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
                return new { success = true, value = SafeToString(prop.GetValue(null)), type = prop.PropertyType.Name };

            return new { success = false, error = $"Static field/property '{fieldName}' not found on type {typeName}" };
        }

        private object SetStaticField(Dictionary<string, string> p)
        {
            var typeName = p.GetValueOrDefault("typeName");
            var fieldName = p.GetValueOrDefault("fieldName");
            var fieldValue = p.GetValueOrDefault("fieldValue");
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(fieldName))
                return new { success = false, error = "typeName and fieldName are required" };

            var type = FindType(typeName);
            if (type == null) return new { success = false, error = $"Type not found: {typeName}" };

            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var converted = ConvertValue(fieldValue, field.FieldType);
                field.SetValue(null, converted);
                return new { success = true, message = $"Set {typeName}.{fieldName} = {fieldValue}" };
            }

            var prop = type.GetProperty(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                var converted = ConvertValue(fieldValue, prop.PropertyType);
                prop.SetValue(null, converted);
                return new { success = true, message = $"Set {typeName}.{fieldName} = {fieldValue}" };
            }

            return new { success = false, error = $"Static field/property '{fieldName}' not found or not writable on {typeName}" };
        }

        private object CallStaticMethod(Dictionary<string, string> p)
        {
            var typeName = p.GetValueOrDefault("typeName");
            var methodName = p.GetValueOrDefault("methodName");
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                return new { success = false, error = "typeName and methodName are required" };

            var type = FindType(typeName);
            if (type == null) return new { success = false, error = $"Type not found: {typeName}" };

            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == methodName).ToArray();

            if (methods.Length == 0)
                return new { success = false, error = $"Static method '{methodName}' not found on {typeName}" };

            var argsJson = p.GetValueOrDefault("methodArgs");
            foreach (var method in methods)
            {
                try
                {
                    var args = ParseMethodArgs(argsJson, method.GetParameters());
                    var result = method.Invoke(null, args);
                    return new { success = true, result = SafeToString(result), resultType = result?.GetType().Name ?? "void" };
                }
                catch { continue; }
            }

            return new { success = false, error = $"Failed to invoke {typeName}.{methodName} with given arguments" };
        }

        private object DebugLog(Dictionary<string, string> p)
        {
            var message = p.GetValueOrDefault("message") ?? p.GetValueOrDefault("expression");
            if (string.IsNullOrEmpty(message))
                return new { success = false, error = "message is required" };

            var debugDataJson = p.GetValueOrDefault("debugData");
            if (!string.IsNullOrEmpty(debugDataJson))
                message += $" | Data: {debugDataJson}";

            Debug.Log($"[MCP Debug] {message}");
            return new { success = true, message = $"Logged: {message}" };
        }

        private object GetComponentValues(Dictionary<string, string> p)
        {
            var go = FindGameObject(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var componentType = p.GetValueOrDefault("componentType");
            if (string.IsNullOrEmpty(componentType))
            {
                // Return all component types
                var types = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().FullName)
                    .ToArray();
                return new { success = true, components = types };
            }

            var type = FindType(componentType);
            if (type == null) return new { success = false, error = $"Type not found: {componentType}" };

            var comp = go.GetComponent(type);
            if (comp == null) return new { success = false, error = $"Component {componentType} not found on {go.name}" };

            var includePrivate = p.GetValueOrDefault("includePrivate")?.ToLower() == "true";
            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (includePrivate) flags |= BindingFlags.NonPublic;

            var serializedValues = new Dictionary<string, object>();
            var so = new SerializedObject(comp);
            var iterator = so.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    serializedValues[iterator.propertyPath] = GetSerializedValue(iterator);
                } while (iterator.NextVisible(false));
            }

            return new
            {
                success = true,
                gameObject = go.name,
                component = componentType,
                serializedFields = serializedValues
            };
        }

        private object GetSerializedValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum: return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue?.name ?? "null";
                case SerializedPropertyType.Vector2: return $"({prop.vector2Value.x}, {prop.vector2Value.y})";
                case SerializedPropertyType.Vector3: return $"({prop.vector3Value.x}, {prop.vector3Value.y}, {prop.vector3Value.z})";
                case SerializedPropertyType.Color: return $"({prop.colorValue.r}, {prop.colorValue.g}, {prop.colorValue.b}, {prop.colorValue.a})";
                case SerializedPropertyType.Rect: return $"({prop.rectValue.x}, {prop.rectValue.y}, {prop.rectValue.width}, {prop.rectValue.height})";
                default: return prop.propertyType.ToString();
            }
        }

        private GameObject FindGameObject(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");

            if (!string.IsNullOrEmpty(path))
                return GameObject.Find(path);
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var instanceId))
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            return null;
        }

        private Type FindType(string name)
        {
            // Direct lookup
            var type = Type.GetType(name);
            if (type != null) return type;

            // Search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(name);
                if (type != null) return type;
            }

            // Try common Unity namespaces
            foreach (var ns in new[] {
                "UnityEngine", "UnityEditor", "UnityEngine.UI", "TMPro",
                "Unity.Netcode", "UnityEngine.EventSystems", "UnityEngine.Rendering",
                "UnityEngine.Audio", "UnityEngine.AI", "UnityEngine.Animations",
                "UnityEditor.SceneManagement", "UnityEngine.SceneManagement",
                "UnityEngine.Profiling", "UnityEngine.Networking",
                "Unity.Netcode.Components", "Unity.Collections"
            })
            {
                type = Type.GetType($"{ns}.{name}, {ns}");
                if (type != null) return type;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType($"{ns}.{name}");
                    if (type != null) return type;
                }
            }

            return null;
        }

        private object[] ParseMethodArgs(string argsJson, ParameterInfo[] parameters)
        {
            if (string.IsNullOrEmpty(argsJson) || argsJson == "[]" || parameters.Length == 0)
                return parameters.Length == 0 ? Array.Empty<object>() : parameters.Select(pr => pr.HasDefaultValue ? pr.DefaultValue : null).ToArray();

            // Simple array parsing
            var trimmed = argsJson.Trim().TrimStart('[').TrimEnd(']');
            var parts = trimmed.Split(',').Select(s => s.Trim().Trim('"')).ToArray();

            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < parts.Length)
                    args[i] = ConvertValue(parts[i], parameters[i].ParameterType);
                else if (parameters[i].HasDefaultValue)
                    args[i] = parameters[i].DefaultValue;
            }
            return args;
        }

        private object ConvertValue(string value, Type targetType)
        {
            if (value == null || value == "null") return null;
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(double)) return double.Parse(value);
            if (targetType == typeof(bool)) return value.ToLower() == "true";
            if (targetType == typeof(long)) return long.Parse(value);
            if (targetType.IsEnum) return Enum.Parse(targetType, value, true);
            return Convert.ChangeType(value, targetType);
        }

        private string SafeToString(object obj)
        {
            if (obj == null) return "null";
            try
            {
                if (obj is IEnumerable enumerable && !(obj is string))
                {
                    var items = new List<string>();
                    var count = 0;
                    foreach (var item in enumerable)
                    {
                        if (count++ > 50) { items.Add("..."); break; }
                        items.Add(item?.ToString() ?? "null");
                    }
                    return "[" + string.Join(", ", items) + "]";
                }
                return obj.ToString();
            }
            catch { return "<error>"; }
        }
    }
}
