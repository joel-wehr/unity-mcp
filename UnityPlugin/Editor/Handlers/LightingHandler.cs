using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class LightingHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "lighting" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "get_settings": return GetSettings();
                case "set_settings": return SetSettings(p);
                case "bake_lighting": return BakeLighting();
                case "cancel_bake": return CancelBake();
                case "get_bake_status": return GetBakeStatus();
                case "clear_baked_data": return ClearBakedData();
                case "set_ambient": return SetAmbient(p);
                case "set_fog": return SetFog(p);
                case "get_light_probes": return GetLightProbes();
                case "get_reflection_probes": return GetReflectionProbes();
                case "set_skybox": return SetSkybox(p);
                case "update_light_probes": return UpdateLightProbes();
                case "render_reflection_probes": return RenderReflectionProbes(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object GetSettings()
        {
            return new
            {
                success = true,
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientColor = ColorStr(RenderSettings.ambientLight),
                ambientSkyColor = ColorStr(RenderSettings.ambientSkyColor),
                ambientEquatorColor = ColorStr(RenderSettings.ambientEquatorColor),
                ambientGroundColor = ColorStr(RenderSettings.ambientGroundColor),
                ambientIntensity = RenderSettings.ambientIntensity,
                fog = RenderSettings.fog,
                fogMode = RenderSettings.fogMode.ToString(),
                fogColor = ColorStr(RenderSettings.fogColor),
                fogDensity = RenderSettings.fogDensity,
                fogStartDistance = RenderSettings.fogStartDistance,
                fogEndDistance = RenderSettings.fogEndDistance,
                skybox = RenderSettings.skybox?.name ?? "none",
                sun = RenderSettings.sun?.name ?? "none",
                subtractiveShadowColor = ColorStr(RenderSettings.subtractiveShadowColor),
                defaultReflectionMode = RenderSettings.defaultReflectionMode.ToString(),
                defaultReflectionResolution = RenderSettings.defaultReflectionResolution,
                reflectionBounces = RenderSettings.reflectionBounces,
                reflectionIntensity = RenderSettings.reflectionIntensity,
                isBaking = Lightmapping.isRunning
            };
        }

        private object SetSettings(Dictionary<string, string> p)
        {
            var settingsJson = p.GetValueOrDefault("lightingSettings");
            if (string.IsNullOrEmpty(settingsJson))
                return new { success = false, error = "lightingSettings object is required" };

            var s = JsonRpcParamsParser.ParseToDictionary(settingsJson);
            var changed = new List<string>();

            if (s.TryGetValue("ambientIntensity", out var ai) && float.TryParse(ai, out var aif))
            { RenderSettings.ambientIntensity = aif; changed.Add("ambientIntensity"); }

            if (s.TryGetValue("reflectionIntensity", out var ri) && float.TryParse(ri, out var rif))
            { RenderSettings.reflectionIntensity = rif; changed.Add("reflectionIntensity"); }

            if (s.TryGetValue("reflectionBounces", out var rb) && int.TryParse(rb, out var rbi))
            { RenderSettings.reflectionBounces = rbi; changed.Add("reflectionBounces"); }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} lighting settings" };
        }

        private object BakeLighting()
        {
            if (Lightmapping.isRunning)
                return new { success = false, error = "Lightmap baking is already running" };

            Lightmapping.BakeAsync();
            return new { success = true, message = "Lightmap baking started" };
        }

        private object CancelBake()
        {
            Lightmapping.Cancel();
            return new { success = true, message = "Lightmap baking cancelled" };
        }

        private object GetBakeStatus()
        {
            return new
            {
                success = true,
                isRunning = Lightmapping.isRunning,
                lightmapCount = LightmapSettings.lightmaps.Length,
                lightProbeCount = LightmapSettings.lightProbes?.count ?? 0
            };
        }

        private object ClearBakedData()
        {
            Lightmapping.Clear();
            Lightmapping.ClearDiskCache();
            return new { success = true, message = "Cleared all baked lighting data" };
        }

        private object SetAmbient(Dictionary<string, string> p)
        {
            var ambientJson = p.GetValueOrDefault("ambientSettings");
            if (string.IsNullOrEmpty(ambientJson))
                return new { success = false, error = "ambientSettings object is required" };

            var s = JsonRpcParamsParser.ParseToDictionary(ambientJson);
            var changed = new List<string>();

            if (s.TryGetValue("mode", out var mode))
            {
                switch (mode.ToLower())
                {
                    case "skybox": RenderSettings.ambientMode = AmbientMode.Skybox; break;
                    case "gradient": RenderSettings.ambientMode = AmbientMode.Trilight; break;
                    case "color": RenderSettings.ambientMode = AmbientMode.Flat; break;
                }
                changed.Add("mode");
            }

            if (s.TryGetValue("ambientIntensity", out var intensity) && float.TryParse(intensity, out var intF))
            { RenderSettings.ambientIntensity = intF; changed.Add("ambientIntensity"); }

            return new { success = true, changed = changed.ToArray() };
        }

        private object SetFog(Dictionary<string, string> p)
        {
            var fogJson = p.GetValueOrDefault("fogSettings");
            if (string.IsNullOrEmpty(fogJson))
                return new { success = false, error = "fogSettings object is required" };

            var s = JsonRpcParamsParser.ParseToDictionary(fogJson);
            var changed = new List<string>();

            if (s.TryGetValue("enabled", out var enabled))
            { RenderSettings.fog = enabled.ToLower() == "true"; changed.Add("enabled"); }

            if (s.TryGetValue("mode", out var mode))
            {
                switch (mode.ToLower())
                {
                    case "linear": RenderSettings.fogMode = FogMode.Linear; break;
                    case "exponential": RenderSettings.fogMode = FogMode.Exponential; break;
                    case "exponentialsquared": RenderSettings.fogMode = FogMode.ExponentialSquared; break;
                }
                changed.Add("mode");
            }

            if (s.TryGetValue("density", out var density) && float.TryParse(density, out var d))
            { RenderSettings.fogDensity = d; changed.Add("density"); }

            if (s.TryGetValue("startDistance", out var start) && float.TryParse(start, out var sf))
            { RenderSettings.fogStartDistance = sf; changed.Add("startDistance"); }

            if (s.TryGetValue("endDistance", out var end) && float.TryParse(end, out var ef))
            { RenderSettings.fogEndDistance = ef; changed.Add("endDistance"); }

            return new { success = true, changed = changed.ToArray() };
        }

        private object GetLightProbes()
        {
            var probes = LightmapSettings.lightProbes;
            return new
            {
                success = true,
                count = probes?.count ?? 0,
                positions = probes?.positions?.Take(20)
                    .Select(v => new { x = v.x, y = v.y, z = v.z }).ToArray()
            };
        }

        private object GetReflectionProbes()
        {
            var probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            var list = probes.Select(rp => new
            {
                name = rp.name,
                position = new { x = rp.transform.position.x, y = rp.transform.position.y, z = rp.transform.position.z },
                size = new { x = rp.size.x, y = rp.size.y, z = rp.size.z },
                mode = rp.mode.ToString(),
                resolution = rp.resolution,
                intensity = rp.intensity
            }).ToList();

            return new { success = true, count = list.Count, probes = list };
        }

        private object SetSkybox(Dictionary<string, string> p)
        {
            var skyboxPath = p.GetValueOrDefault("skyboxPath");
            if (string.IsNullOrEmpty(skyboxPath))
                return new { success = false, error = "skyboxPath is required" };

            var mat = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (mat == null)
                return new { success = false, error = $"Material not found at path: {skyboxPath}" };

            if (!mat.shader.name.Contains("Skybox"))
                return new { success = false, error = $"Material '{mat.name}' does not use a Skybox shader (uses '{mat.shader.name}')" };

            RenderSettings.skybox = mat;
            DynamicGI.UpdateEnvironment();
            return new { success = true, message = $"Skybox set to '{mat.name}'", shaderName = mat.shader.name };
        }

        private object UpdateLightProbes()
        {
            var probes = LightmapSettings.lightProbes;
            if (probes == null || probes.count == 0)
                return new { success = false, error = "No light probes found in the scene" };

            LightProbes.Tetrahedralize();
            return new { success = true, message = $"Light probes re-tetrahedralized ({probes.count} probes)" };
        }

        private object RenderReflectionProbes(Dictionary<string, string> p)
        {
            var probeIdStr = p.GetValueOrDefault("reflectionProbeId");
            ReflectionProbe[] probes;

            if (!string.IsNullOrEmpty(probeIdStr) && int.TryParse(probeIdStr, out var probeId))
            {
                var obj = McpId.ToObject(probeId);
                var probe = (obj as GameObject)?.GetComponent<ReflectionProbe>();
                if (probe == null)
                    return new { success = false, error = $"ReflectionProbe not found with instance ID: {probeId}" };
                probes = new[] { probe };
            }
            else
            {
                probes = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            }

            if (probes.Length == 0)
                return new { success = false, error = "No ReflectionProbe objects found in scene" };

            var rendered = new List<string>();
            foreach (var probe in probes)
            {
                if (probe.mode == ReflectionProbeMode.Realtime || probe.mode == ReflectionProbeMode.Custom)
                {
                    probe.RenderProbe();
                    rendered.Add(probe.name);
                }
            }

            return new
            {
                success = true,
                renderedCount = rendered.Count,
                totalProbes = probes.Length,
                rendered = rendered.ToArray(),
                message = $"Rendered {rendered.Count} reflection probe(s)"
            };
        }

        private string ColorStr(Color c) => $"({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
    }
}
