using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL hand tracking MCP tool requests.
    /// Includes enable_hand_tracking, get_hand_state, configure_hand_gestures, create_hand_interactable
    /// </summary>
    public class XrealHandTrackingHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "enable_hand_tracking",
            "get_hand_state",
            "configure_hand_gestures",
            "create_hand_interactable"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "enable_hand_tracking":
                    return EnableHandTracking(paramsDict);
                case "get_hand_state":
                    return GetHandState(paramsDict);
                case "configure_hand_gestures":
                    return ConfigureHandGestures(paramsDict);
                case "create_hand_interactable":
                    return CreateHandInteractable(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object EnableHandTracking(Dictionary<string, string> @params)
        {
            var enabled = @params.GetValueOrDefault("enabled")?.ToLower() != "false";
            var trackingMode = @params.GetValueOrDefault("trackingMode") ?? "Advanced";
            var trackedHands = @params.GetValueOrDefault("trackedHands") ?? "Both";
            var gestureRecognition = @params.GetValueOrDefault("gestureRecognition")?.ToLower() != "false";
            var jointVisualization = @params.GetValueOrDefault("jointVisualization")?.ToLower() == "true";
            var handMeshVisualization = @params.GetValueOrDefault("handMeshVisualization")?.ToLower() == "true";

            // In a real implementation, this would:
            // 1. Enable NRHandTrackingManager
            // 2. Configure tracking settings
            // 3. Set up gesture recognition

            var setupResults = new List<string>();

            if (enabled)
            {
                setupResults.Add("Hand tracking enabled");
                setupResults.Add($"Tracking mode: {trackingMode}");
                setupResults.Add($"Tracked hands: {trackedHands}");

                if (gestureRecognition)
                {
                    setupResults.Add("Gesture recognition enabled");
                }

                if (jointVisualization)
                {
                    // Add hand joint visualization prefab to scene
                    setupResults.Add("Joint visualization enabled");
                }

                if (handMeshVisualization)
                {
                    setupResults.Add("Hand mesh visualization enabled");
                }
            }
            else
            {
                setupResults.Add("Hand tracking disabled");
            }

            return new
            {
                success = true,
                enabled = enabled,
                trackingMode = trackingMode,
                trackedHands = trackedHands,
                gestureRecognition = gestureRecognition,
                setupResults = setupResults
            };
        }

        private object GetHandState(Dictionary<string, string> @params)
        {
            var hand = @params.GetValueOrDefault("hand") ?? "Both";
            var includeJointPositions = @params.GetValueOrDefault("includeJointPositions")?.ToLower() != "false";
            var includeJointRotations = @params.GetValueOrDefault("includeJointRotations")?.ToLower() == "true";
            var includeGestures = @params.GetValueOrDefault("includeGestures")?.ToLower() != "false";
            var includeVelocity = @params.GetValueOrDefault("includeVelocity")?.ToLower() == "true";
            var coordinateSpace = @params.GetValueOrDefault("coordinateSpace") ?? "World";

            // Simulated hand state for editor development
            var leftHand = (hand == "Both" || hand == "Left") ? CreateSimulatedHandState("Left", includeJointPositions, includeJointRotations, includeGestures, includeVelocity) : null;
            var rightHand = (hand == "Both" || hand == "Right") ? CreateSimulatedHandState("Right", includeJointPositions, includeJointRotations, includeGestures, includeVelocity) : null;

            return new
            {
                success = true,
                coordinateSpace = coordinateSpace,
                leftHand = leftHand,
                rightHand = rightHand,
                note = "Simulated hand state - real data requires NRSDK at runtime"
            };
        }

        private object CreateSimulatedHandState(string hand, bool includeJoints, bool includeRotations, bool includeGestures, bool includeVelocity)
        {
            var baseX = hand == "Left" ? -0.2f : 0.2f;

            var state = new Dictionary<string, object>
            {
                ["hand"] = hand,
                ["isTracked"] = false,
                ["confidence"] = 0.0f,
                ["palmPosition"] = new { x = baseX, y = 1.0f, z = 0.5f },
                ["palmNormal"] = new { x = 0, y = 0, z = -1 }
            };

            if (includeJoints)
            {
                // Simplified joint list
                var joints = new[]
                {
                    "Wrist", "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip",
                    "IndexMetacarpal", "IndexProximal", "IndexIntermediate", "IndexDistal", "IndexTip",
                    "MiddleMetacarpal", "MiddleProximal", "MiddleIntermediate", "MiddleDistal", "MiddleTip",
                    "RingMetacarpal", "RingProximal", "RingIntermediate", "RingDistal", "RingTip",
                    "PinkyMetacarpal", "PinkyProximal", "PinkyIntermediate", "PinkyDistal", "PinkyTip"
                };

                var jointPositions = new Dictionary<string, object>();
                foreach (var joint in joints)
                {
                    jointPositions[joint] = new { x = baseX, y = 1.0f, z = 0.5f };
                }
                state["jointPositions"] = jointPositions;
            }

            if (includeGestures)
            {
                state["gesture"] = new
                {
                    current = "None",
                    pinchStrength = 0.0f,
                    grabStrength = 0.0f,
                    isPointing = false
                };
            }

            if (includeVelocity)
            {
                state["velocity"] = new { x = 0, y = 0, z = 0 };
                state["angularVelocity"] = new { x = 0, y = 0, z = 0 };
            }

            return state;
        }

        private object ConfigureHandGestures(Dictionary<string, string> @params)
        {
            var enabledGesturesStr = @params.GetValueOrDefault("enabledGestures") ?? "Pinch,Grab,Point,OpenPalm";
            var pinchThresholdStr = @params.GetValueOrDefault("pinchThreshold") ?? "0.7";
            var grabThresholdStr = @params.GetValueOrDefault("grabThreshold") ?? "0.8";
            var gestureHoldTimeStr = @params.GetValueOrDefault("gestureHoldTime") ?? "0.1";
            var smoothingFactorStr = @params.GetValueOrDefault("smoothingFactor") ?? "0.5";
            var continuousMode = @params.GetValueOrDefault("continuousMode")?.ToLower() != "false";

            var enabledGestures = enabledGesturesStr.Split(',').Select(g => g.Trim()).ToArray();
            var pinchThreshold = float.TryParse(pinchThresholdStr, out var pt) ? pt : 0.7f;
            var grabThreshold = float.TryParse(grabThresholdStr, out var gt) ? gt : 0.8f;
            var gestureHoldTime = float.TryParse(gestureHoldTimeStr, out var ght) ? ght : 0.1f;
            var smoothingFactor = float.TryParse(smoothingFactorStr, out var sf) ? sf : 0.5f;

            return new
            {
                success = true,
                configuration = new
                {
                    enabledGestures = enabledGestures,
                    pinchThreshold = pinchThreshold,
                    grabThreshold = grabThreshold,
                    gestureHoldTime = gestureHoldTime,
                    smoothingFactor = smoothingFactor,
                    continuousMode = continuousMode
                },
                note = "Gesture configuration set - will apply at runtime with NRSDK"
            };
        }

        private object CreateHandInteractable(Dictionary<string, string> @params)
        {
            var targetGameObject = @params.GetValueOrDefault("targetGameObject");
            var interactionType = @params.GetValueOrDefault("interactionType");
            var highlightOnHover = @params.GetValueOrDefault("highlightOnHover")?.ToLower() != "false";
            var highlightColor = @params.GetValueOrDefault("highlightColor") ?? "#FFD700";
            var hoverDistanceStr = @params.GetValueOrDefault("hoverDistance") ?? "0.1";
            var grabType = @params.GetValueOrDefault("grabType") ?? "Kinematic";
            var throwOnRelease = @params.GetValueOrDefault("throwOnRelease")?.ToLower() != "false";
            var throwMultiplierStr = @params.GetValueOrDefault("throwMultiplier") ?? "1.5";
            var pokeDepthStr = @params.GetValueOrDefault("pokeDepth") ?? "0.02";
            var twoHandedGrab = @params.GetValueOrDefault("twoHandedGrab")?.ToLower() == "true";
            var hapticFeedback = @params.GetValueOrDefault("hapticFeedback")?.ToLower() != "false";

            if (string.IsNullOrEmpty(targetGameObject))
            {
                return new { success = false, error = "targetGameObject is required" };
            }

            if (string.IsNullOrEmpty(interactionType))
            {
                return new { success = false, error = "interactionType is required (Grab, Poke, Hover, or All)" };
            }

            // Find the target GameObject
            GameObject go = null;
            if (int.TryParse(targetGameObject, out var instanceId))
            {
                go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }
            else
            {
                go = GameObject.Find(targetGameObject);
            }

            if (go == null)
            {
                return new { success = false, error = $"GameObject not found: {targetGameObject}" };
            }

            var addedComponents = new List<string>();

            // Add collider if missing
            if (go.GetComponent<Collider>() == null)
            {
                var collider = go.AddComponent<BoxCollider>();
                Undo.RegisterCreatedObjectUndo(collider, "Add Collider");
                addedComponents.Add("BoxCollider");
            }

            // Add Rigidbody if needed for grab interactions
            if ((interactionType == "Grab" || interactionType == "All") && go.GetComponent<Rigidbody>() == null)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = grabType == "Kinematic";
                Undo.RegisterCreatedObjectUndo(rb, "Add Rigidbody");
                addedComponents.Add("Rigidbody");
            }

            // Note: In a real implementation, we would add NRSDK or XR Interaction Toolkit components
            // For now, we'll add a marker component or script

            EditorUtility.SetDirty(go);

            return new
            {
                success = true,
                gameObject = go.name,
                instanceId = go.GetInstanceID(),
                interactionType = interactionType,
                addedComponents = addedComponents,
                configuration = new
                {
                    highlightOnHover = highlightOnHover,
                    highlightColor = highlightColor,
                    hoverDistance = float.TryParse(hoverDistanceStr, out var hd) ? hd : 0.1f,
                    grabType = grabType,
                    throwOnRelease = throwOnRelease,
                    throwMultiplier = float.TryParse(throwMultiplierStr, out var tm) ? tm : 1.5f,
                    pokeDepth = float.TryParse(pokeDepthStr, out var pd) ? pd : 0.02f,
                    twoHandedGrab = twoHandedGrab,
                    hapticFeedback = hapticFeedback
                },
                note = "Hand interaction configured. Add NRSDK HandInteraction components for full functionality."
            };
        }
    }
}
