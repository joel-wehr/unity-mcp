using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    public class PhysicsHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "physics" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "raycast": return Raycast(p);
                case "raycast_all": return RaycastAll(p);
                case "spherecast": return SphereCast(p);
                case "overlap_sphere": return OverlapSphere(p);
                case "overlap_box": return OverlapBox(p);
                case "set_gravity": return SetGravity(p);
                case "get_rigidbody_state": return GetRigidbodyState(p);
                case "add_force": return AddForce(p);
                case "set_velocity": return SetVelocity(p);
                case "get_layer_collision": return GetLayerCollision(p);
                case "set_layer_collision": return SetLayerCollision(p);
                case "boxcast": return BoxCast(p);
                case "simulate": return Simulate(p);
                case "add_torque": return AddTorque(p);
                case "get_contacts": return GetContacts(p);
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        private Vector3 ParseVector3(Dictionary<string, string> p, string prefix)
        {
            float.TryParse(p.GetValueOrDefault($"{prefix}X") ?? p.GetValueOrDefault($"{prefix}.x") ?? "0", out var x);
            float.TryParse(p.GetValueOrDefault($"{prefix}Y") ?? p.GetValueOrDefault($"{prefix}.y") ?? "0", out var y);
            float.TryParse(p.GetValueOrDefault($"{prefix}Z") ?? p.GetValueOrDefault($"{prefix}.z") ?? "0", out var z);
            return new Vector3(x, y, z);
        }

        private Vector3 ParseVector3FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return Vector3.zero;
            var d = JsonRpcParamsParser.ParseToDictionary(json);
            float.TryParse(d.GetValueOrDefault("x") ?? "0", out var x);
            float.TryParse(d.GetValueOrDefault("y") ?? "0", out var y);
            float.TryParse(d.GetValueOrDefault("z") ?? "0", out var z);
            return new Vector3(x, y, z);
        }

        private object Raycast(Dictionary<string, string> p)
        {
            var origin = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var direction = ParseVector3FromJson(p.GetValueOrDefault("direction"));
            var maxDist = float.TryParse(p.GetValueOrDefault("maxDistance") ?? "1000", out var md) ? md : 1000f;

            if (Physics.Raycast(origin, direction, out var hit, maxDist))
            {
                return new
                {
                    success = true,
                    didHit = true,
                    hitPoint = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                    hitNormal = new { x = hit.normal.x, y = hit.normal.y, z = hit.normal.z },
                    distance = hit.distance,
                    colliderName = hit.collider.name,
                    gameObjectName = hit.collider.gameObject.name
                };
            }
            return new { success = true, didHit = false };
        }

        private object RaycastAll(Dictionary<string, string> p)
        {
            var origin = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var direction = ParseVector3FromJson(p.GetValueOrDefault("direction"));
            var maxDist = float.TryParse(p.GetValueOrDefault("maxDistance") ?? "1000", out var md) ? md : 1000f;

            var hits = Physics.RaycastAll(origin, direction, maxDist)
                .Select(h => new
                {
                    point = new { x = h.point.x, y = h.point.y, z = h.point.z },
                    distance = h.distance,
                    colliderName = h.collider.name,
                    gameObjectName = h.collider.gameObject.name
                }).ToList();

            return new { success = true, hitCount = hits.Count, hits = hits };
        }

        private object SphereCast(Dictionary<string, string> p)
        {
            var origin = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var direction = ParseVector3FromJson(p.GetValueOrDefault("direction"));
            var radius = float.TryParse(p.GetValueOrDefault("radius") ?? "1", out var r) ? r : 1f;
            var maxDist = float.TryParse(p.GetValueOrDefault("maxDistance") ?? "1000", out var md) ? md : 1000f;

            if (Physics.SphereCast(origin, radius, direction, out var hit, maxDist))
            {
                return new
                {
                    success = true,
                    didHit = true,
                    hitPoint = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                    distance = hit.distance,
                    colliderName = hit.collider.name
                };
            }
            return new { success = true, didHit = false };
        }

        private object OverlapSphere(Dictionary<string, string> p)
        {
            var center = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var radius = float.TryParse(p.GetValueOrDefault("radius") ?? "1", out var r) ? r : 1f;
            var maxStr = p.GetValueOrDefault("maxResults") ?? "10";
            var max = int.TryParse(maxStr, out var m) ? m : 10;

            var colliders = Physics.OverlapSphere(center, radius)
                .Take(max)
                .Select(c => new { name = c.name, gameObject = c.gameObject.name, type = c.GetType().Name })
                .ToList();

            return new { success = true, count = colliders.Count, colliders = colliders };
        }

        private object OverlapBox(Dictionary<string, string> p)
        {
            var center = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var halfExtents = ParseVector3FromJson(p.GetValueOrDefault("halfExtents"));

            var colliders = Physics.OverlapBox(center, halfExtents)
                .Take(10)
                .Select(c => new { name = c.name, gameObject = c.gameObject.name })
                .ToList();

            return new { success = true, count = colliders.Count, colliders = colliders };
        }

        private object SetGravity(Dictionary<string, string> p)
        {
            var gravity = ParseVector3FromJson(p.GetValueOrDefault("gravity"));
            Physics.gravity = gravity;
            return new { success = true, gravity = new { x = gravity.x, y = gravity.y, z = gravity.z } };
        }

        private object GetRigidbodyState(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) return new { success = false, error = "No Rigidbody on object" };

#if UNITY_6000_0_OR_NEWER
            var linVel = rb.linearVelocity;
            var dragVal = rb.linearDamping;
            var angDragVal = rb.angularDamping;
#else
            var linVel = rb.velocity;
            var dragVal = rb.drag;
            var angDragVal = rb.angularDrag;
#endif
            return new
            {
                success = true,
                mass = rb.mass,
                drag = dragVal,
                angularDrag = angDragVal,
                useGravity = rb.useGravity,
                isKinematic = rb.isKinematic,
                velocity = new { x = linVel.x, y = linVel.y, z = linVel.z },
                angularVelocity = new { x = rb.angularVelocity.x, y = rb.angularVelocity.y, z = rb.angularVelocity.z },
                position = new { x = rb.position.x, y = rb.position.y, z = rb.position.z }
            };
        }

        private object AddForce(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) return new { success = false, error = "No Rigidbody on object" };

            var force = ParseVector3FromJson(p.GetValueOrDefault("force"));
            var modeStr = p.GetValueOrDefault("forceMode") ?? "Force";
            var mode = Enum.TryParse<ForceMode>(modeStr, true, out var fm) ? fm : ForceMode.Force;

            rb.AddForce(force, mode);
            return new { success = true, message = $"Applied force ({force.x}, {force.y}, {force.z}) mode={mode}" };
        }

        private object SetVelocity(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) return new { success = false, error = "No Rigidbody on object" };

            var vel = ParseVector3FromJson(p.GetValueOrDefault("velocity"));
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
            return new { success = true, message = $"Set velocity to ({vel.x}, {vel.y}, {vel.z})" };
        }

        private object GetLayerCollision(Dictionary<string, string> p)
        {
            var l1Str = p.GetValueOrDefault("layer1") ?? "0";
            var l2Str = p.GetValueOrDefault("layer2") ?? "0";
            var l1 = int.TryParse(l1Str, out var li1) ? li1 : 0;
            var l2 = int.TryParse(l2Str, out var li2) ? li2 : 0;

            var ignores = Physics.GetIgnoreLayerCollision(l1, l2);
            return new
            {
                success = true,
                layer1 = l1,
                layer1Name = LayerMask.LayerToName(l1),
                layer2 = l2,
                layer2Name = LayerMask.LayerToName(l2),
                collisionEnabled = !ignores
            };
        }

        private object SetLayerCollision(Dictionary<string, string> p)
        {
            var l1 = int.TryParse(p.GetValueOrDefault("layer1") ?? "0", out var li1) ? li1 : 0;
            var l2 = int.TryParse(p.GetValueOrDefault("layer2") ?? "0", out var li2) ? li2 : 0;
            var collide = p.GetValueOrDefault("collide")?.ToLower() != "false";

            Physics.IgnoreLayerCollision(l1, l2, !collide);
            return new { success = true, message = $"Set layer {l1}<->{l2} collision to {collide}" };
        }

        private object BoxCast(Dictionary<string, string> p)
        {
            var center = ParseVector3FromJson(p.GetValueOrDefault("origin"));
            var halfExtents = ParseVector3FromJson(p.GetValueOrDefault("halfExtents"));
            var direction = ParseVector3FromJson(p.GetValueOrDefault("direction"));
            var maxDist = float.TryParse(p.GetValueOrDefault("maxDistance") ?? "1000", out var md) ? md : 1000f;

            if (Physics.BoxCast(center, halfExtents, direction, out var hit, Quaternion.identity, maxDist))
            {
                return new
                {
                    success = true,
                    didHit = true,
                    hitPoint = new { x = hit.point.x, y = hit.point.y, z = hit.point.z },
                    hitNormal = new { x = hit.normal.x, y = hit.normal.y, z = hit.normal.z },
                    distance = hit.distance,
                    colliderName = hit.collider.name,
                    gameObjectName = hit.collider.gameObject.name
                };
            }
            return new { success = true, didHit = false };
        }

        private object Simulate(Dictionary<string, string> p)
        {
            var stepsStr = p.GetValueOrDefault("simulationSteps") ?? "1";
            var steps = int.TryParse(stepsStr, out var s) ? Mathf.Clamp(s, 1, 100) : 1;

            if (Physics.simulationMode == SimulationMode.Script)
            {
                for (int i = 0; i < steps; i++)
                {
                    Physics.Simulate(Time.fixedDeltaTime);
                }
                return new { success = true, message = $"Simulated {steps} physics step(s)", fixedDeltaTime = Time.fixedDeltaTime };
            }

            return new
            {
                success = false,
                error = "Physics.simulationMode must be set to Script for manual simulation. " +
                        $"Current mode: {Physics.simulationMode}. " +
                        "Change via project_settings or Physics.simulationMode = SimulationMode.Script."
            };
        }

        private object AddTorque(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) return new { success = false, error = "No Rigidbody on object" };

            var torque = ParseVector3FromJson(p.GetValueOrDefault("force")); // reuse force param for torque vector
            var modeStr = p.GetValueOrDefault("forceMode") ?? "Force";
            var mode = Enum.TryParse<ForceMode>(modeStr, true, out var fm) ? fm : ForceMode.Force;

            rb.AddTorque(torque, mode);
            return new { success = true, message = $"Applied torque ({torque.x}, {torque.y}, {torque.z}) mode={mode}" };
        }

        private object GetContacts(Dictionary<string, string> p)
        {
            var go = FindGO(p);
            if (go == null) return new { success = false, error = "Object not found" };

            var collider = go.GetComponent<Collider>();
            if (collider == null) return new { success = false, error = "No Collider on object" };

            var contactPoints = new ContactPoint[20];
            var rb = go.GetComponent<Rigidbody>();
            int count = 0;

            // ContactPoints are only available during collision callbacks in play mode
            // In editor, we can check if there's a rigidbody and report its state
            if (rb != null)
            {
                return new
                {
                    success = true,
                    message = "Contact points are only available during play mode collision callbacks. Showing collider info instead.",
                    colliderType = collider.GetType().Name,
                    bounds = new
                    {
                        center = new { x = collider.bounds.center.x, y = collider.bounds.center.y, z = collider.bounds.center.z },
                        size = new { x = collider.bounds.size.x, y = collider.bounds.size.y, z = collider.bounds.size.z }
                    },
                    isTrigger = collider.isTrigger,
#if UNITY_6000_0_OR_NEWER
                    rigidbodyVelocity = new { x = rb.linearVelocity.x, y = rb.linearVelocity.y, z = rb.linearVelocity.z },
#else
                    rigidbodyVelocity = new { x = rb.velocity.x, y = rb.velocity.y, z = rb.velocity.z },
#endif
                    isSleeping = rb.IsSleeping()
                };
            }

            return new
            {
                success = true,
                colliderType = collider.GetType().Name,
                bounds = new
                {
                    center = new { x = collider.bounds.center.x, y = collider.bounds.center.y, z = collider.bounds.center.z },
                    size = new { x = collider.bounds.size.x, y = collider.bounds.size.y, z = collider.bounds.size.z }
                },
                isTrigger = collider.isTrigger
            };
        }

        private GameObject FindGO(Dictionary<string, string> p)
        {
            var path = p.GetValueOrDefault("objectPath");
            var id = p.GetValueOrDefault("objectId");
            if (!string.IsNullOrEmpty(path)) return GameObject.Find(path);
            if (!string.IsNullOrEmpty(id) && int.TryParse(id, out var iid))
                return McpId.ToObject(iid) as GameObject;
            return null;
        }
    }
}
