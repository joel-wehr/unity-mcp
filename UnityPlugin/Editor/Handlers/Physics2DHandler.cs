using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Handles 2D physics: Rigidbody2D, Collider2D, raycasts, joints, effectors.
    /// </summary>
    public class Physics2DHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "physics2d" };

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
                case "raycast": return Raycast2D(p);
                case "overlap_circle": return OverlapCircle(p);
                case "overlap_box": return OverlapBox(p);
                case "get_rigidbodies": return GetRigidbodies();
                case "set_rigidbody": return SetRigidbody(p);
                case "add_force": return AddForce2D(p);
                case "get_colliders": return GetColliders();
                case "add_collider": return AddCollider(p);
                case "get_joints": return GetJoints();
                case "get_effectors": return GetEffectors();
                case "get_layers": return GetCollisionLayers();
                case "set_layer_collision": return SetLayerCollision(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private object GetSettings()
        {
            return new
            {
                success = true,
                gravity = new { x = Physics2D.gravity.x, y = Physics2D.gravity.y },
                defaultContactOffset = Physics2D.defaultContactOffset,
                velocityIterations = Physics2D.velocityIterations,
                positionIterations = Physics2D.positionIterations,
                queriesHitTriggers = Physics2D.queriesHitTriggers,
                queriesStartInColliders = Physics2D.queriesStartInColliders,
                simulationMode = Physics2D.simulationMode.ToString()
            };
        }

        private object SetSettings(Dictionary<string, string> p)
        {
            var changed = new List<string>();

            if (p.TryGetValue("gravityX", out var gx) && p.TryGetValue("gravityY", out var gy))
            {
                if (float.TryParse(gx, out var gxf) && float.TryParse(gy, out var gyf))
                { Physics2D.gravity = new Vector2(gxf, gyf); changed.Add("gravity"); }
            }

            if (p.TryGetValue("defaultContactOffset", out var dco) && float.TryParse(dco, out var dcof))
            { Physics2D.defaultContactOffset = dcof; changed.Add("defaultContactOffset"); }

            if (p.TryGetValue("velocityIterations", out var vi) && int.TryParse(vi, out var vii))
            { Physics2D.velocityIterations = vii; changed.Add("velocityIterations"); }

            if (p.TryGetValue("positionIterations", out var pi) && int.TryParse(pi, out var pii))
            { Physics2D.positionIterations = pii; changed.Add("positionIterations"); }

            if (p.TryGetValue("queriesHitTriggers", out var qht))
            { Physics2D.queriesHitTriggers = qht.ToLower() == "true"; changed.Add("queriesHitTriggers"); }

            if (p.TryGetValue("queriesStartInColliders", out var qsic))
            { Physics2D.queriesStartInColliders = qsic.ToLower() == "true"; changed.Add("queriesStartInColliders"); }

            return new { success = true, changed = changed.ToArray(), message = $"Updated {changed.Count} Physics2D settings" };
        }

        private object Raycast2D(Dictionary<string, string> p)
        {
            float.TryParse(p.GetValueOrDefault("originX") ?? "0", out var ox);
            float.TryParse(p.GetValueOrDefault("originY") ?? "0", out var oy);
            float.TryParse(p.GetValueOrDefault("dirX") ?? "1", out var dx);
            float.TryParse(p.GetValueOrDefault("dirY") ?? "0", out var dy);
            float.TryParse(p.GetValueOrDefault("distance") ?? "100", out var dist);

            var origin = new Vector2(ox, oy);
            var direction = new Vector2(dx, dy).normalized;

            var hit = Physics2D.Raycast(origin, direction, dist);

            if (hit.collider != null)
            {
                return new
                {
                    success = true,
                    hit = true,
                    point = new { x = hit.point.x, y = hit.point.y },
                    normal = new { x = hit.normal.x, y = hit.normal.y },
                    distance = hit.distance,
                    colliderName = hit.collider.gameObject.name,
                    colliderInstanceId = McpId.Get(hit.collider.gameObject)
                };
            }

            return new { success = true, hit = false };
        }

        private object OverlapCircle(Dictionary<string, string> p)
        {
            float.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            float.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);
            float.TryParse(p.GetValueOrDefault("radius") ?? "1", out var radius);

            var results = Physics2D.OverlapCircleAll(new Vector2(x, y), radius);
            var list = results.Take(50).Select(c => new
            {
                name = c.gameObject.name,
                instanceId = McpId.Get(c.gameObject),
                colliderType = c.GetType().Name,
                isTrigger = c.isTrigger
            }).ToList();

            return new { success = true, count = list.Count, colliders = list };
        }

        private object OverlapBox(Dictionary<string, string> p)
        {
            float.TryParse(p.GetValueOrDefault("x") ?? "0", out var x);
            float.TryParse(p.GetValueOrDefault("y") ?? "0", out var y);
            float.TryParse(p.GetValueOrDefault("sizeX") ?? "1", out var sx);
            float.TryParse(p.GetValueOrDefault("sizeY") ?? "1", out var sy);
            float.TryParse(p.GetValueOrDefault("angle") ?? "0", out var angle);

            var results = Physics2D.OverlapBoxAll(new Vector2(x, y), new Vector2(sx, sy), angle);
            var list = results.Take(50).Select(c => new
            {
                name = c.gameObject.name,
                instanceId = McpId.Get(c.gameObject),
                colliderType = c.GetType().Name,
                isTrigger = c.isTrigger
            }).ToList();

            return new { success = true, count = list.Count, colliders = list };
        }

        private object GetRigidbodies()
        {
            var rbs = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            var list = rbs.Select(rb => new
            {
                name = rb.gameObject.name,
                instanceId = McpId.Get(rb.gameObject),
                bodyType = rb.bodyType.ToString(),
                mass = rb.mass,
#if UNITY_6000_0_OR_NEWER
                linearDamping = rb.linearDamping,
                angularDamping = rb.angularDamping,
#else
                linearDamping = rb.drag,
                angularDamping = rb.angularDrag,
#endif
                gravityScale = rb.gravityScale,
                position = new { x = rb.position.x, y = rb.position.y },
                rotation = rb.rotation,
                simulated = rb.simulated,
                constraints = rb.constraints.ToString()
            }).ToList();

            return new { success = true, count = list.Count, rigidbodies = list };
        }

        private object SetRigidbody(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = Undo.AddComponent<Rigidbody2D>(go);

            var changed = new List<string>();

            if (p.TryGetValue("bodyType", out var bt))
            {
                switch (bt.ToLower())
                {
                    case "dynamic": rb.bodyType = RigidbodyType2D.Dynamic; break;
                    case "kinematic": rb.bodyType = RigidbodyType2D.Kinematic; break;
                    case "static": rb.bodyType = RigidbodyType2D.Static; break;
                }
                changed.Add("bodyType");
            }

            if (p.TryGetValue("mass", out var m) && float.TryParse(m, out var mf))
            { rb.mass = mf; changed.Add("mass"); }

            if (p.TryGetValue("gravityScale", out var gs) && float.TryParse(gs, out var gsf))
            { rb.gravityScale = gsf; changed.Add("gravityScale"); }

            if (p.TryGetValue("linearDamping", out var ld) && float.TryParse(ld, out var ldf))
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = ldf;
#else
                rb.drag = ldf;
#endif
                changed.Add("linearDamping");
            }

            if (p.TryGetValue("angularDamping", out var ad) && float.TryParse(ad, out var adf))
            {
#if UNITY_6000_0_OR_NEWER
                rb.angularDamping = adf;
#else
                rb.angularDrag = adf;
#endif
                changed.Add("angularDamping");
            }

            if (p.TryGetValue("simulated", out var sim))
            { rb.simulated = sim.ToLower() == "true"; changed.Add("simulated"); }

            EditorUtility.SetDirty(go);
            return new { success = true, changed = changed.ToArray(), message = $"Rigidbody2D configured on '{go.name}'" };
        }

        private object AddForce2D(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
                return new { success = false, error = $"'{go.name}' has no Rigidbody2D" };

            float.TryParse(p.GetValueOrDefault("forceX") ?? "0", out var fx);
            float.TryParse(p.GetValueOrDefault("forceY") ?? "0", out var fy);

            var modeStr = p.GetValueOrDefault("forceMode") ?? "Force";
            var mode = ForceMode2D.Force;
            if (modeStr.ToLower() == "impulse") mode = ForceMode2D.Impulse;

            rb.AddForce(new Vector2(fx, fy), mode);
            return new { success = true, message = $"Applied force ({fx},{fy}) mode={mode} to '{go.name}'" };
        }

        private object GetColliders()
        {
            var colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            var list = colliders.Take(100).Select(c => new
            {
                name = c.gameObject.name,
                instanceId = McpId.Get(c.gameObject),
                type = c.GetType().Name,
                isTrigger = c.isTrigger,
                enabled = c.enabled,
                bounds = new
                {
                    center = new { x = c.bounds.center.x, y = c.bounds.center.y },
                    size = new { x = c.bounds.size.x, y = c.bounds.size.y }
                }
            }).ToList();

            return new { success = true, count = list.Count, colliders = list };
        }

        private object AddCollider(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null)
                return new { success = false, error = "objectPath or objectId required" };

            var colliderType = p.GetValueOrDefault("colliderType") ?? "box";
            var isTrigger = p.GetValueOrDefault("isTrigger")?.ToLower() == "true";

            Collider2D collider = null;
            switch (colliderType.ToLower())
            {
                case "box":
                    collider = Undo.AddComponent<BoxCollider2D>(go);
                    break;
                case "circle":
                    collider = Undo.AddComponent<CircleCollider2D>(go);
                    if (p.TryGetValue("radius", out var r) && float.TryParse(r, out var rf))
                        ((CircleCollider2D)collider).radius = rf;
                    break;
                case "capsule":
                    collider = Undo.AddComponent<CapsuleCollider2D>(go);
                    break;
                case "polygon":
                    collider = Undo.AddComponent<PolygonCollider2D>(go);
                    break;
                case "edge":
                    collider = Undo.AddComponent<EdgeCollider2D>(go);
                    break;
                default:
                    return new { success = false, error = $"Unknown collider type: {colliderType}. Use: box, circle, capsule, polygon, edge" };
            }

            if (collider != null)
                collider.isTrigger = isTrigger;

            EditorUtility.SetDirty(go);
            return new { success = true, message = $"Added {colliderType} Collider2D to '{go.name}'", isTrigger = isTrigger };
        }

        private object GetJoints()
        {
            var joints = UnityEngine.Object.FindObjectsByType<Joint2D>(FindObjectsSortMode.None);
            var list = joints.Take(50).Select(j => new
            {
                name = j.gameObject.name,
                instanceId = McpId.Get(j.gameObject),
                type = j.GetType().Name,
                enabled = j.enabled,
                connectedBody = j.connectedBody?.gameObject.name ?? "none",
                breakForce = j.breakForce,
                breakTorque = j.breakTorque
            }).ToList();

            return new { success = true, count = list.Count, joints = list };
        }

        private object GetEffectors()
        {
            var effectors = UnityEngine.Object.FindObjectsByType<Effector2D>(FindObjectsSortMode.None);
            var list = effectors.Take(50).Select(e => new
            {
                name = e.gameObject.name,
                instanceId = McpId.Get(e.gameObject),
                type = e.GetType().Name,
                enabled = e.enabled,
                useColliderMask = e.useColliderMask
            }).ToList();

            return new { success = true, count = list.Count, effectors = list };
        }

        private object GetCollisionLayers()
        {
            var layers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    var collidesWith = new List<string>();
                    for (int j = 0; j < 32; j++)
                    {
                        var otherName = LayerMask.LayerToName(j);
                        if (!string.IsNullOrEmpty(otherName) && !Physics2D.GetIgnoreLayerCollision(i, j))
                            collidesWith.Add(otherName);
                    }
                    layers.Add(new { index = i, name = name, collidesWith = collidesWith.ToArray() });
                }
            }
            return new { success = true, layers = layers };
        }

        private object SetLayerCollision(Dictionary<string, string> p)
        {
            var layer1Str = p.GetValueOrDefault("layer1");
            var layer2Str = p.GetValueOrDefault("layer2");
            var ignoreStr = p.GetValueOrDefault("ignore") ?? "true";

            if (string.IsNullOrEmpty(layer1Str) || string.IsNullOrEmpty(layer2Str))
                return new { success = false, error = "layer1 and layer2 are required (layer names or indices)" };

            int layer1, layer2;
            if (!int.TryParse(layer1Str, out layer1))
                layer1 = LayerMask.NameToLayer(layer1Str);
            if (!int.TryParse(layer2Str, out layer2))
                layer2 = LayerMask.NameToLayer(layer2Str);

            if (layer1 < 0 || layer2 < 0)
                return new { success = false, error = "Invalid layer name or index" };

            var ignore = ignoreStr.ToLower() == "true";
            Physics2D.IgnoreLayerCollision(layer1, layer2, ignore);

            return new
            {
                success = true,
                message = $"{(ignore ? "Ignoring" : "Enabling")} collision between layer {layer1} and {layer2}"
            };
        }

        private GameObject FindGO(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");
            if (!string.IsNullOrEmpty(path)) return GameObject.Find(path);
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var iid))
                return McpId.ToObject(iid) as GameObject;
            return Selection.activeGameObject;
        }
    }
}
