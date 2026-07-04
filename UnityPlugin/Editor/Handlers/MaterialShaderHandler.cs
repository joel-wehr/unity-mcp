using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class MaterialShaderHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "material_shader" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_material": return GetMaterial(p);
                case "set_material_property": return SetMaterialProperty(p);
                case "create_material": return CreateMaterial(p);
                case "assign_material": return AssignMaterial(p);
                case "get_shader_properties": return GetShaderProperties(p);
                case "get_available_shaders": return GetAvailableShaders(p);
                case "set_shader": return SetShader(p);
                case "copy_material": return CopyMaterial(p);
                case "get_keywords": return GetKeywords(p);
                case "enable_keyword": return EnableKeyword(p);
                case "disable_keyword": return DisableKeyword(p);
                case "get_global_shader_property": return GetGlobalShaderProperty(p);
                case "set_global_shader_property": return SetGlobalShaderProperty(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private Renderer FindRenderer(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");
            GameObject go = null;
            if (!string.IsNullOrEmpty(path)) go = GameObject.Find(path);
            else if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var iid))
                go = McpId.ToObject(iid) as GameObject;
            return go?.GetComponent<Renderer>();
        }

        private Material GetMat(Dictionary<string, string> p)
        {
            var matPath = p.GetValueOrDefault("materialPath");
            if (!string.IsNullOrEmpty(matPath))
                return AssetDatabase.LoadAssetAtPath<Material>(matPath);

            var renderer = FindRenderer(p);
            if (renderer == null) return null;

            var indexStr = p.GetValueOrDefault("materialIndex") ?? "0";
            var index = int.TryParse(indexStr, out var i) ? i : 0;
            var useShared = p.GetValueOrDefault("useSharedMaterial")?.ToLower() != "false";

            var mats = useShared ? renderer.sharedMaterials : renderer.materials;
            return index < mats.Length ? mats[index] : null;
        }

        private object GetMaterial(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };

            var shader = mat.shader;
            var propCount = shader.GetPropertyCount();
            var properties = new List<object>();

            for (int i = 0; i < propCount; i++)
            {
                var propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);
                string value = propType switch
                {
                    UnityEngine.Rendering.ShaderPropertyType.Color => mat.GetColor(propName).ToString(),
                    UnityEngine.Rendering.ShaderPropertyType.Float => mat.GetFloat(propName).ToString(),
                    UnityEngine.Rendering.ShaderPropertyType.Range => mat.GetFloat(propName).ToString(),
                    UnityEngine.Rendering.ShaderPropertyType.Vector => mat.GetVector(propName).ToString(),
                    UnityEngine.Rendering.ShaderPropertyType.Texture => mat.GetTexture(propName)?.name ?? "null",
                    _ => "unknown"
                };

                properties.Add(new
                {
                    name = propName,
                    type = propType.ToString(),
                    description = shader.GetPropertyDescription(i),
                    value = value
                });
            }

            return new
            {
                success = true,
                name = mat.name,
                shader = shader.name,
                renderQueue = mat.renderQueue,
                passCount = mat.passCount,
                keywords = mat.shaderKeywords,
                properties = properties
            };
        }

        private object SetMaterialProperty(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };

            var propName = p.GetValueOrDefault("propertyName");
            var propType = p.GetValueOrDefault("propertyType") ?? "float";
            if (string.IsNullOrEmpty(propName))
                return new { success = false, error = "propertyName is required" };

            switch (propType.ToLower())
            {
                case "float":
                case "range":
                    if (float.TryParse(p.GetValueOrDefault("propertyValue"), out var fv))
                        mat.SetFloat(propName, fv);
                    break;
                case "color":
                    var colorJson = p.GetValueOrDefault("colorValue");
                    if (!string.IsNullOrEmpty(colorJson))
                    {
                        var cd = JsonRpcParamsParser.ParseToDictionary(colorJson);
                        float.TryParse(cd.GetValueOrDefault("r") ?? "1", out var r);
                        float.TryParse(cd.GetValueOrDefault("g") ?? "1", out var g);
                        float.TryParse(cd.GetValueOrDefault("b") ?? "1", out var b);
                        float.TryParse(cd.GetValueOrDefault("a") ?? "1", out var a);
                        mat.SetColor(propName, new Color(r, g, b, a));
                    }
                    break;
                case "vector":
                    var vecJson = p.GetValueOrDefault("vectorValue");
                    if (!string.IsNullOrEmpty(vecJson))
                    {
                        var vd = JsonRpcParamsParser.ParseToDictionary(vecJson);
                        float.TryParse(vd.GetValueOrDefault("x") ?? "0", out var vx);
                        float.TryParse(vd.GetValueOrDefault("y") ?? "0", out var vy);
                        float.TryParse(vd.GetValueOrDefault("z") ?? "0", out var vz);
                        float.TryParse(vd.GetValueOrDefault("w") ?? "0", out var vw);
                        mat.SetVector(propName, new Vector4(vx, vy, vz, vw));
                    }
                    break;
                case "texture":
                    var texPath = p.GetValueOrDefault("texturePath");
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                        if (tex != null) mat.SetTexture(propName, tex);
                    }
                    break;
            }

            EditorUtility.SetDirty(mat);
            return new { success = true, message = $"Set {propName} on {mat.name}" };
        }

        private object CreateMaterial(Dictionary<string, string> p)
        {
            var shaderName = p.GetValueOrDefault("shaderName") ?? "Universal Render Pipeline/Lit";
            var matPath = p.GetValueOrDefault("newMaterialPath");
            if (string.IsNullOrEmpty(matPath))
                return new { success = false, error = "newMaterialPath is required" };

            var shader = Shader.Find(shaderName);
            if (shader == null) return new { success = false, error = $"Shader not found: {shaderName}" };

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            AssetDatabase.SaveAssets();

            return new { success = true, path = matPath, shader = shaderName };
        }

        private object AssignMaterial(Dictionary<string, string> p)
        {
            var renderer = FindRenderer(p);
            if (renderer == null) return new { success = false, error = "Renderer not found" };

            var matPath = p.GetValueOrDefault("materialPath");
            if (string.IsNullOrEmpty(matPath))
                return new { success = false, error = "materialPath is required" };

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) return new { success = false, error = $"Material not found at {matPath}" };

            var indexStr = p.GetValueOrDefault("materialIndex") ?? "0";
            var index = int.TryParse(indexStr, out var i) ? i : 0;

            var mats = renderer.sharedMaterials;
            if (index < mats.Length)
            {
                mats[index] = mat;
                renderer.sharedMaterials = mats;
                EditorUtility.SetDirty(renderer);
                return new { success = true, message = $"Assigned {mat.name} to slot {index}" };
            }

            return new { success = false, error = $"Material index {index} out of range" };
        }

        private object GetShaderProperties(Dictionary<string, string> p)
        {
            var shaderName = p.GetValueOrDefault("shaderName");
            if (string.IsNullOrEmpty(shaderName))
                return new { success = false, error = "shaderName is required" };

            var shader = Shader.Find(shaderName);
            if (shader == null) return new { success = false, error = $"Shader not found: {shaderName}" };

            var props = new List<object>();
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                props.Add(new
                {
                    name = shader.GetPropertyName(i),
                    type = shader.GetPropertyType(i).ToString(),
                    description = shader.GetPropertyDescription(i)
                });
            }

            return new { success = true, shader = shaderName, properties = props };
        }

        private object GetAvailableShaders(Dictionary<string, string> p)
        {
            var filter = p.GetValueOrDefault("searchFilter") ?? "";
            var shaderInfos = ShaderUtil.GetAllShaderInfo()
                .Where(s => string.IsNullOrEmpty(filter) || s.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(50)
                .Select(s => new { name = s.name, supported = s.supported })
                .ToList();

            return new { success = true, count = shaderInfos.Count, shaders = shaderInfos };
        }

        private object SetShader(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };

            var shaderName = p.GetValueOrDefault("shaderName");
            if (string.IsNullOrEmpty(shaderName))
                return new { success = false, error = "shaderName is required" };

            var shader = Shader.Find(shaderName);
            if (shader == null) return new { success = false, error = $"Shader not found: {shaderName}" };

            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return new { success = true, message = $"Set shader to {shaderName} on {mat.name}" };
        }

        private object CopyMaterial(Dictionary<string, string> p)
        {
            var sourcePath = p.GetValueOrDefault("sourceMaterialPath");
            var destPath = p.GetValueOrDefault("newMaterialPath");
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
                return new { success = false, error = "sourceMaterialPath and newMaterialPath are required" };

            var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null) return new { success = false, error = $"Source material not found: {sourcePath}" };

            var copy = new Material(source);
            AssetDatabase.CreateAsset(copy, destPath);
            AssetDatabase.SaveAssets();

            return new { success = true, source = sourcePath, destination = destPath };
        }

        private object GetKeywords(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };
            return new { success = true, keywords = mat.shaderKeywords };
        }

        private object EnableKeyword(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };
            var keyword = p.GetValueOrDefault("keyword");
            if (string.IsNullOrEmpty(keyword)) return new { success = false, error = "keyword is required" };
            mat.EnableKeyword(keyword);
            EditorUtility.SetDirty(mat);
            return new { success = true, message = $"Enabled keyword: {keyword}" };
        }

        private object DisableKeyword(Dictionary<string, string> p)
        {
            var mat = GetMat(p);
            if (mat == null) return new { success = false, error = "Material not found" };
            var keyword = p.GetValueOrDefault("keyword");
            if (string.IsNullOrEmpty(keyword)) return new { success = false, error = "keyword is required" };
            mat.DisableKeyword(keyword);
            EditorUtility.SetDirty(mat);
            return new { success = true, message = $"Disabled keyword: {keyword}" };
        }

        private object GetGlobalShaderProperty(Dictionary<string, string> p)
        {
            var propName = p.GetValueOrDefault("propertyName");
            if (string.IsNullOrEmpty(propName))
                return new { success = false, error = "propertyName is required" };

            var propType = p.GetValueOrDefault("propertyType") ?? "float";

            switch (propType.ToLower())
            {
                case "float":
                    var fv = Shader.GetGlobalFloat(propName);
                    return new { success = true, propertyName = propName, type = "float", value = fv.ToString() };
                case "int":
                    var iv = Shader.GetGlobalInt(propName);
                    return new { success = true, propertyName = propName, type = "int", value = iv.ToString() };
                case "color":
                    var cv = Shader.GetGlobalColor(propName);
                    return new { success = true, propertyName = propName, type = "color", value = cv.ToString() };
                case "vector":
                    var vv = Shader.GetGlobalVector(propName);
                    return new { success = true, propertyName = propName, type = "vector", value = vv.ToString() };
                case "texture":
                    var tv = Shader.GetGlobalTexture(propName);
                    return new { success = true, propertyName = propName, type = "texture", value = tv?.name ?? "null" };
                default:
                    return new { success = false, error = $"Unknown property type: {propType}" };
            }
        }

        private object SetGlobalShaderProperty(Dictionary<string, string> p)
        {
            var propName = p.GetValueOrDefault("propertyName");
            if (string.IsNullOrEmpty(propName))
                return new { success = false, error = "propertyName is required" };

            var propType = p.GetValueOrDefault("propertyType") ?? "float";

            switch (propType.ToLower())
            {
                case "float":
                    if (float.TryParse(p.GetValueOrDefault("propertyValue"), out var fv))
                    {
                        Shader.SetGlobalFloat(propName, fv);
                        return new { success = true, message = $"Set global float {propName} = {fv}" };
                    }
                    return new { success = false, error = "Invalid float value" };
                case "int":
                    if (int.TryParse(p.GetValueOrDefault("propertyValue"), out var iv))
                    {
                        Shader.SetGlobalInt(propName, iv);
                        return new { success = true, message = $"Set global int {propName} = {iv}" };
                    }
                    return new { success = false, error = "Invalid int value" };
                case "color":
                    var colorJson = p.GetValueOrDefault("colorValue");
                    if (!string.IsNullOrEmpty(colorJson))
                    {
                        var cd = JsonRpcParamsParser.ParseToDictionary(colorJson);
                        float.TryParse(cd.GetValueOrDefault("r") ?? "1", out var r);
                        float.TryParse(cd.GetValueOrDefault("g") ?? "1", out var g);
                        float.TryParse(cd.GetValueOrDefault("b") ?? "1", out var b);
                        float.TryParse(cd.GetValueOrDefault("a") ?? "1", out var a);
                        Shader.SetGlobalColor(propName, new Color(r, g, b, a));
                        return new { success = true, message = $"Set global color {propName}" };
                    }
                    return new { success = false, error = "colorValue is required" };
                case "vector":
                    var vecJson = p.GetValueOrDefault("vectorValue");
                    if (!string.IsNullOrEmpty(vecJson))
                    {
                        var vd = JsonRpcParamsParser.ParseToDictionary(vecJson);
                        float.TryParse(vd.GetValueOrDefault("x") ?? "0", out var vx);
                        float.TryParse(vd.GetValueOrDefault("y") ?? "0", out var vy);
                        float.TryParse(vd.GetValueOrDefault("z") ?? "0", out var vz);
                        float.TryParse(vd.GetValueOrDefault("w") ?? "0", out var vw);
                        Shader.SetGlobalVector(propName, new Vector4(vx, vy, vz, vw));
                        return new { success = true, message = $"Set global vector {propName}" };
                    }
                    return new { success = false, error = "vectorValue is required" };
                case "texture":
                    var texPath = p.GetValueOrDefault("texturePath");
                    if (!string.IsNullOrEmpty(texPath))
                    {
                        var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                        if (tex != null)
                        {
                            Shader.SetGlobalTexture(propName, tex);
                            return new { success = true, message = $"Set global texture {propName}" };
                        }
                        return new { success = false, error = $"Texture not found: {texPath}" };
                    }
                    return new { success = false, error = "texturePath is required" };
                default:
                    return new { success = false, error = $"Unknown property type: {propType}" };
            }
        }
    }
}
