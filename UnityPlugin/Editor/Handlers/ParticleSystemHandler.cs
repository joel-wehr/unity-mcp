using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles ParticleSystem inspection and configuration.
    /// </summary>
    public class ParticleSystemHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "particle_system" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "create": return CreateParticleSystem(p);
                case "get_info": return GetInfo(p);
                case "set_main": return SetMain(p);
                case "set_emission": return SetEmission(p);
                case "set_shape": return SetShape(p);
                case "set_renderer": return SetRenderer(p);
                case "play": return PlayControl(p, "play");
                case "stop": return PlayControl(p, "stop");
                case "pause": return PlayControl(p, "pause");
                case "restart": return PlayControl(p, "restart");
                case "list": return ListParticleSystems();
                case "set_color_over_lifetime": return SetColorOverLifetime(p);
                case "set_size_over_lifetime": return SetSizeOverLifetime(p);
                case "set_velocity_over_lifetime": return SetVelocityOverLifetime(p);
                case "get_modules": return GetModules(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private ParticleSystem FindPS(Dictionary<string, string> p)
        {
            var objectPath = p.GetValueOrDefault("objectPath");
            if (!string.IsNullOrEmpty(objectPath))
            {
                var go = GameObject.Find(objectPath);
                if (go != null) return go.GetComponent<ParticleSystem>();
            }

            var objectId = p.GetValueOrDefault("objectId");
            if (!string.IsNullOrEmpty(objectId) && int.TryParse(objectId, out var id))
            {
                var obj = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (obj != null) return obj.GetComponent<ParticleSystem>();
            }

            return Selection.activeGameObject?.GetComponent<ParticleSystem>();
        }

        private object CreateParticleSystem(Dictionary<string, string> p)
        {
            var name = p.GetValueOrDefault("name") ?? "Particle System";
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();

            // Optional position
            float.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            float.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);
            float.TryParse(p.GetValueOrDefault("z") ?? "0", out var z);
            go.transform.position = new Vector3(x, y, z);

            // Optional parent
            var parentPath = p.GetValueOrDefault("parentPath");
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObject.Find(parentPath);
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create Particle System {name}");

            return new
            {
                success = true,
                instanceId = go.GetInstanceID(),
                name = go.name,
                message = $"Created particle system '{name}'"
            };
        }

        private object GetInfo(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            return new
            {
                success = true,
                name = ps.gameObject.name,
                instanceId = ps.gameObject.GetInstanceID(),
                isPlaying = ps.isPlaying,
                particleCount = ps.particleCount,
                main = new
                {
                    duration = main.duration,
                    looping = main.loop,
                    startDelay = main.startDelay.constant,
                    startLifetime = main.startLifetime.constant,
                    startSpeed = main.startSpeed.constant,
                    startSize = main.startSize.constant,
                    startColor = ColorStr(main.startColor.color),
                    maxParticles = main.maxParticles,
                    simulationSpace = main.simulationSpace.ToString(),
                    gravityModifier = main.gravityModifier.constant,
                    playOnAwake = main.playOnAwake,
                    emitterVelocityMode = main.emitterVelocityMode.ToString()
                },
                emission = new
                {
                    enabled = emission.enabled,
                    rateOverTime = emission.rateOverTime.constant,
                    rateOverDistance = emission.rateOverDistance.constant,
                    burstCount = emission.burstCount
                },
                shape = new
                {
                    enabled = shape.enabled,
                    shapeType = shape.shapeType.ToString(),
                    radius = shape.radius,
                    angle = shape.angle,
                    arc = shape.arc
                },
                rendererMode = renderer?.renderMode.ToString() ?? "none",
                material = renderer?.sharedMaterial?.name ?? "none"
            };
        }

        private object SetMain(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var main = ps.main;
            var changed = new List<string>();

            if (p.TryGetValue("duration", out var dur) && float.TryParse(dur, out var durf))
            { main.duration = durf; changed.Add("duration"); }

            if (p.TryGetValue("looping", out var loop))
            { main.loop = loop.ToLower() == "true"; changed.Add("looping"); }

            if (p.TryGetValue("startLifetime", out var sl) && float.TryParse(sl, out var slf))
            { main.startLifetime = slf; changed.Add("startLifetime"); }

            if (p.TryGetValue("startSpeed", out var ss) && float.TryParse(ss, out var ssf))
            { main.startSpeed = ssf; changed.Add("startSpeed"); }

            if (p.TryGetValue("startSize", out var sz) && float.TryParse(sz, out var szf))
            { main.startSize = szf; changed.Add("startSize"); }

            if (p.TryGetValue("maxParticles", out var mp) && int.TryParse(mp, out var mpi))
            { main.maxParticles = mpi; changed.Add("maxParticles"); }

            if (p.TryGetValue("gravityModifier", out var gm) && float.TryParse(gm, out var gmf))
            { main.gravityModifier = gmf; changed.Add("gravityModifier"); }

            if (p.TryGetValue("playOnAwake", out var poa))
            { main.playOnAwake = poa.ToLower() == "true"; changed.Add("playOnAwake"); }

            if (p.TryGetValue("simulationSpace", out var space))
            {
                switch (space.ToLower())
                {
                    case "local": main.simulationSpace = ParticleSystemSimulationSpace.Local; break;
                    case "world": main.simulationSpace = ParticleSystemSimulationSpace.World; break;
                    case "custom": main.simulationSpace = ParticleSystemSimulationSpace.Custom; break;
                }
                changed.Add("simulationSpace");
            }

            EditorUtility.SetDirty(ps);
            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} main module properties" };
        }

        private object SetEmission(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var emission = ps.emission;
            var changed = new List<string>();

            if (p.TryGetValue("enabled", out var en))
            { emission.enabled = en.ToLower() == "true"; changed.Add("enabled"); }

            if (p.TryGetValue("rateOverTime", out var rot) && float.TryParse(rot, out var rotf))
            { emission.rateOverTime = rotf; changed.Add("rateOverTime"); }

            if (p.TryGetValue("rateOverDistance", out var rod) && float.TryParse(rod, out var rodf))
            { emission.rateOverDistance = rodf; changed.Add("rateOverDistance"); }

            EditorUtility.SetDirty(ps);
            return new { success = true, changed = changed.ToArray() };
        }

        private object SetShape(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var shape = ps.shape;
            var changed = new List<string>();

            if (p.TryGetValue("enabled", out var en))
            { shape.enabled = en.ToLower() == "true"; changed.Add("enabled"); }

            if (p.TryGetValue("shapeType", out var st))
            {
                if (Enum.TryParse<ParticleSystemShapeType>(st, true, out var shapeType))
                { shape.shapeType = shapeType; changed.Add("shapeType"); }
            }

            if (p.TryGetValue("radius", out var r) && float.TryParse(r, out var rf))
            { shape.radius = rf; changed.Add("radius"); }

            if (p.TryGetValue("angle", out var a) && float.TryParse(a, out var af))
            { shape.angle = af; changed.Add("angle"); }

            if (p.TryGetValue("arc", out var arc) && float.TryParse(arc, out var arcf))
            { shape.arc = arcf; changed.Add("arc"); }

            EditorUtility.SetDirty(ps);
            return new { success = true, changed = changed.ToArray() };
        }

        private object SetRenderer(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
                return new { success = false, error = "No ParticleSystemRenderer found" };

            var changed = new List<string>();

            if (p.TryGetValue("renderMode", out var rm))
            {
                if (Enum.TryParse<ParticleSystemRenderMode>(rm, true, out var mode))
                { renderer.renderMode = mode; changed.Add("renderMode"); }
            }

            if (p.TryGetValue("materialPath", out var mp))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (mat != null) { renderer.sharedMaterial = mat; changed.Add("material"); }
            }

            if (p.TryGetValue("sortingOrder", out var so) && int.TryParse(so, out var soi))
            { renderer.sortingOrder = soi; changed.Add("sortingOrder"); }

            EditorUtility.SetDirty(ps);
            return new { success = true, changed = changed.ToArray() };
        }

        private object PlayControl(Dictionary<string, string> p, string command)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            switch (command)
            {
                case "play": ps.Play(); break;
                case "stop": ps.Stop(); break;
                case "pause": ps.Pause(); break;
                case "restart": ps.Stop(); ps.Clear(); ps.Play(); break;
            }

            return new { success = true, message = $"ParticleSystem '{ps.gameObject.name}' — {command}", particleCount = ps.particleCount };
        }

        private object ListParticleSystems()
        {
            var systems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            var list = systems.Select(ps => new
            {
                name = ps.gameObject.name,
                instanceId = ps.gameObject.GetInstanceID(),
                isPlaying = ps.isPlaying,
                particleCount = ps.particleCount,
                maxParticles = ps.main.maxParticles,
                looping = ps.main.loop,
                duration = ps.main.duration
            }).ToList();

            return new { success = true, count = list.Count, particleSystems = list };
        }

        private object SetColorOverLifetime(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var col = ps.colorOverLifetime;
            col.enabled = true;

            // Simple start/end color
            var startColorStr = p.GetValueOrDefault("startColor");
            var endColorStr = p.GetValueOrDefault("endColor");

            if (!string.IsNullOrEmpty(startColorStr) && !string.IsNullOrEmpty(endColorStr))
            {
                var startColor = ParseColor(startColorStr);
                var endColor = ParseColor(endColorStr);

                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                    new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(endColor.a, 1f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(gradient);
            }

            EditorUtility.SetDirty(ps);
            return new { success = true, message = "Set color over lifetime" };
        }

        private object SetSizeOverLifetime(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;

            float.TryParse(p.GetValueOrDefault("startSize") ?? "1", out var startSize);
            float.TryParse(p.GetValueOrDefault("endSize") ?? "0", out var endSize);

            var curve = new AnimationCurve(
                new Keyframe(0f, startSize),
                new Keyframe(1f, endSize)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            EditorUtility.SetDirty(ps);
            return new { success = true, message = $"Set size over lifetime: {startSize} -> {endSize}" };
        }

        private object SetVelocityOverLifetime(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;

            if (p.TryGetValue("xMin", out var xmin) && p.TryGetValue("xMax", out var xmax))
            {
                float.TryParse(xmin, out var xminf);
                float.TryParse(xmax, out var xmaxf);
                vol.x = new ParticleSystem.MinMaxCurve(xminf, xmaxf);
            }

            if (p.TryGetValue("yMin", out var ymin) && p.TryGetValue("yMax", out var ymax))
            {
                float.TryParse(ymin, out var yminf);
                float.TryParse(ymax, out var ymaxf);
                vol.y = new ParticleSystem.MinMaxCurve(yminf, ymaxf);
            }

            if (p.TryGetValue("zMin", out var zmin) && p.TryGetValue("zMax", out var zmax))
            {
                float.TryParse(zmin, out var zminf);
                float.TryParse(zmax, out var zmaxf);
                vol.z = new ParticleSystem.MinMaxCurve(zminf, zmaxf);
            }

            EditorUtility.SetDirty(ps);
            return new { success = true, message = "Set velocity over lifetime" };
        }

        private object GetModules(Dictionary<string, string> p)
        {
            var ps = FindPS(p);
            if (ps == null)
                return new { success = false, error = "No ParticleSystem found" };

            return new
            {
                success = true,
                modules = new
                {
                    emission = ps.emission.enabled,
                    shape = ps.shape.enabled,
                    velocityOverLifetime = ps.velocityOverLifetime.enabled,
                    colorOverLifetime = ps.colorOverLifetime.enabled,
                    sizeOverLifetime = ps.sizeOverLifetime.enabled,
                    rotationOverLifetime = ps.rotationOverLifetime.enabled,
                    noise = ps.noise.enabled,
                    collision = ps.collision.enabled,
                    trigger = ps.trigger.enabled,
                    subEmitters = ps.subEmitters.enabled,
                    textureSheetAnimation = ps.textureSheetAnimation.enabled,
                    lights = ps.lights.enabled,
                    trails = ps.trails.enabled,
                    forceOverLifetime = ps.forceOverLifetime.enabled,
                    limitVelocityOverLifetime = ps.limitVelocityOverLifetime.enabled,
                    inheritVelocity = ps.inheritVelocity.enabled,
                    externalForces = ps.externalForces.enabled
                }
            };
        }

        private Color ParseColor(string colorJson)
        {
            var cp = JsonRpcParamsParser.ParseToDictionary(colorJson);
            float.TryParse(cp.GetValueOrDefault("r") ?? "1", out var r);
            float.TryParse(cp.GetValueOrDefault("g") ?? "1", out var g);
            float.TryParse(cp.GetValueOrDefault("b") ?? "1", out var b);
            float.TryParse(cp.GetValueOrDefault("a") ?? "1", out var a);
            return new Color(r, g, b, a);
        }

        private string ColorStr(Color c) => $"({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
    }
}
