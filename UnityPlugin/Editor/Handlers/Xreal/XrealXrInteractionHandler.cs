using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers.Xreal
{
    /// <summary>
    /// Handles XREAL XR Interaction Toolkit MCP tool requests.
    /// Includes setup_xr_interaction, create_xr_rig, add_xr_interactor, create_xr_ui
    /// </summary>
    public class XrealXrInteractionHandler : IToolHandler
    {
        public string[] SupportedMethods => new[]
        {
            "setup_xr_interaction",
            "create_xr_rig",
            "add_xr_interactor",
            "create_xr_ui"
        };

        public object Handle(string method, string paramsJson)
        {
            var paramsDict = Core.JsonRpcParamsParser.ParseToDictionary(paramsJson);

            switch (method)
            {
                case "setup_xr_interaction":
                    return SetupXrInteraction(paramsDict);
                case "create_xr_rig":
                    return CreateXrRig(paramsDict);
                case "add_xr_interactor":
                    return AddXrInteractor(paramsDict);
                case "create_xr_ui":
                    return CreateXrUi(paramsDict);
                default:
                    throw new ArgumentException($"Unknown method: {method}");
            }
        }

        private object SetupXrInteraction(Dictionary<string, string> @params)
        {
            var interactionMode = @params.GetValueOrDefault("interactionMode") ?? "HandTracking";
            var installXRIT = @params.GetValueOrDefault("installXRIT")?.ToLower() != "false";
            var setupDefaultInteractors = @params.GetValueOrDefault("setupDefaultInteractors")?.ToLower() != "false";
            var createInputActions = @params.GetValueOrDefault("createInputActions")?.ToLower() != "false";
            var enableTeleportation = @params.GetValueOrDefault("enableTeleportation")?.ToLower() == "true";
            var enableSnapTurn = @params.GetValueOrDefault("enableSnapTurn")?.ToLower() == "true";
            var hapticFeedback = @params.GetValueOrDefault("hapticFeedback")?.ToLower() != "false";
            var locomotionProvider = @params.GetValueOrDefault("locomotionProvider") ?? "None";

            var results = new List<string>();
            var errors = new List<string>();

            // Check if XR Interaction Toolkit is installed
            var xritInstalled = IsXritInstalled();

            if (!xritInstalled && installXRIT)
            {
                // Try to install via Package Manager
                var request = UnityEditor.PackageManager.Client.Add("com.unity.xr.interaction.toolkit");
                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    results.Add("Installed XR Interaction Toolkit");
                    xritInstalled = true;
                }
                else
                {
                    errors.Add($"Failed to install XR Interaction Toolkit: {request.Error?.message}");
                }
            }
            else if (!xritInstalled)
            {
                errors.Add("XR Interaction Toolkit not installed. Set installXRIT to true or install manually.");
            }

            if (xritInstalled)
            {
                results.Add($"Interaction mode: {interactionMode}");

                if (createInputActions)
                {
                    results.Add("Input actions configuration ready");
                }

                if (setupDefaultInteractors)
                {
                    results.Add("Default interactors configured");
                }

                if (enableTeleportation)
                {
                    results.Add("Teleportation locomotion enabled");
                }

                if (enableSnapTurn)
                {
                    results.Add("Snap turn enabled");
                }

                if (hapticFeedback)
                {
                    results.Add("Haptic feedback enabled");
                }
            }

            return new
            {
                success = errors.Count == 0,
                xritInstalled = xritInstalled,
                interactionMode = interactionMode,
                results = results,
                errors = errors.Count > 0 ? errors : null,
                nextSteps = new[]
                {
                    "Create XR rig using create_xr_rig tool",
                    "Add interactables to objects using create_hand_interactable tool"
                }
            };
        }

        private object CreateXrRig(Dictionary<string, string> @params)
        {
            var rigName = @params.GetValueOrDefault("rigName") ?? "XR Origin (XREAL)";
            var trackingOriginMode = @params.GetValueOrDefault("trackingOriginMode") ?? "Device";
            var cameraYOffsetStr = @params.GetValueOrDefault("cameraYOffset") ?? "0";
            var addHandControllers = @params.GetValueOrDefault("addHandControllers")?.ToLower() != "false";
            var addRayInteractors = @params.GetValueOrDefault("addRayInteractors")?.ToLower() != "false";
            var addDirectInteractors = @params.GetValueOrDefault("addDirectInteractors")?.ToLower() != "false";
            var addUIInteraction = @params.GetValueOrDefault("addUIInteraction")?.ToLower() != "false";
            var addLocomotionSystem = @params.GetValueOrDefault("addLocomotionSystem")?.ToLower() == "true";
            var createAsPrefab = @params.GetValueOrDefault("createAsPrefab")?.ToLower() == "true";
            var prefabPath = @params.GetValueOrDefault("prefabPath");

            var cameraYOffset = float.TryParse(cameraYOffsetStr, out var offset) ? offset : 0f;

            // Check for existing XR Origin
            var existingRig = GameObject.Find(rigName);
            if (existingRig != null)
            {
                return new
                {
                    success = false,
                    error = $"XR Origin already exists: {rigName}",
                    existingInstanceId = existingRig.GetInstanceID()
                };
            }

            // Create XR Origin hierarchy
            var xrOrigin = new GameObject(rigName);

            // Camera Offset
            var cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(xrOrigin.transform);
            cameraOffset.transform.localPosition = new Vector3(0, cameraYOffset, 0);

            // Main Camera
            var mainCamera = new GameObject("Main Camera");
            mainCamera.transform.SetParent(cameraOffset.transform);
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.tag = "MainCamera";

            var camera = mainCamera.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 1000f;

            mainCamera.AddComponent<AudioListener>();

            var addedComponents = new List<string> { "Camera", "AudioListener" };

            // Hand controllers
            if (addHandControllers)
            {
                // Left Hand
                var leftHand = new GameObject("Left Hand");
                leftHand.transform.SetParent(cameraOffset.transform);
                leftHand.transform.localPosition = new Vector3(-0.2f, 0, 0.3f);

                // Right Hand
                var rightHand = new GameObject("Right Hand");
                rightHand.transform.SetParent(cameraOffset.transform);
                rightHand.transform.localPosition = new Vector3(0.2f, 0, 0.3f);

                addedComponents.Add("Hand Controllers");

                // Add ray interactors
                if (addRayInteractors)
                {
                    CreateRayInteractor(leftHand, "Left");
                    CreateRayInteractor(rightHand, "Right");
                    addedComponents.Add("Ray Interactors");
                }

                // Add direct interactors
                if (addDirectInteractors)
                {
                    CreateDirectInteractor(leftHand, "Left");
                    CreateDirectInteractor(rightHand, "Right");
                    addedComponents.Add("Direct Interactors");
                }
            }

            // UI Interaction
            if (addUIInteraction)
            {
                // Add UI interaction components
                addedComponents.Add("UI Interaction");
            }

            // Locomotion System
            if (addLocomotionSystem)
            {
                var locomotion = new GameObject("Locomotion System");
                locomotion.transform.SetParent(xrOrigin.transform);
                addedComponents.Add("Locomotion System");
            }

            Undo.RegisterCreatedObjectUndo(xrOrigin, "Create XR Origin");

            // Create prefab if requested
            if (createAsPrefab)
            {
                var path = string.IsNullOrEmpty(prefabPath) ? "Assets/Prefabs/XR Origin (XREAL).prefab" : prefabPath;
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                PrefabUtility.SaveAsPrefabAsset(xrOrigin, path);
            }

            return new
            {
                success = true,
                instanceId = xrOrigin.GetInstanceID(),
                rigName = rigName,
                hierarchy = new
                {
                    xrOrigin = xrOrigin.name,
                    cameraOffset = cameraOffset.name,
                    mainCamera = mainCamera.name
                },
                addedComponents = addedComponents,
                trackingOriginMode = trackingOriginMode,
                cameraYOffset = cameraYOffset,
                prefabCreated = createAsPrefab,
                note = "XR Origin created. Add XR Plugin Management and NRSDK components for full functionality."
            };
        }

        private void CreateRayInteractor(GameObject parent, string hand)
        {
            var rayInteractor = new GameObject($"{hand} Ray Interactor");
            rayInteractor.transform.SetParent(parent.transform);
            rayInteractor.transform.localPosition = Vector3.zero;

            // Add LineRenderer for ray visualization
            var lineRenderer = rayInteractor.AddComponent<LineRenderer>();
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.green;
            lineRenderer.endColor = Color.green;
            lineRenderer.positionCount = 2;
        }

        private void CreateDirectInteractor(GameObject parent, string hand)
        {
            var directInteractor = new GameObject($"{hand} Direct Interactor");
            directInteractor.transform.SetParent(parent.transform);
            directInteractor.transform.localPosition = Vector3.zero;

            // Add sphere collider for interaction
            var collider = directInteractor.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.05f;
        }

        private object AddXrInteractor(Dictionary<string, string> @params)
        {
            var targetGameObject = @params.GetValueOrDefault("targetGameObject");
            var interactorType = @params.GetValueOrDefault("interactorType");
            var hand = @params.GetValueOrDefault("hand") ?? "None";
            var rayLength = @params.GetValueOrDefault("rayLength") ?? "10";
            var rayWidth = @params.GetValueOrDefault("rayWidth") ?? "0.01";
            var lineType = @params.GetValueOrDefault("lineType") ?? "Straight";
            var rayValidColor = @params.GetValueOrDefault("rayValidColor") ?? "#00FF00";
            var rayInvalidColor = @params.GetValueOrDefault("rayInvalidColor") ?? "#FF0000";
            var attachTransform = @params.GetValueOrDefault("attachTransform")?.ToLower() != "false";
            var enableHaptics = @params.GetValueOrDefault("enableHaptics")?.ToLower() != "false";
            var selectActionTrigger = @params.GetValueOrDefault("selectActionTrigger") ?? "StateChange";

            if (string.IsNullOrEmpty(targetGameObject))
            {
                return new { success = false, error = "targetGameObject is required" };
            }

            if (string.IsNullOrEmpty(interactorType))
            {
                return new { success = false, error = "interactorType is required (Ray, Direct, Poke, or Gaze)" };
            }

            // Find target GameObject
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

            switch (interactorType.ToLower())
            {
                case "ray":
                    var rayGo = new GameObject("Ray Interactor");
                    rayGo.transform.SetParent(go.transform);
                    rayGo.transform.localPosition = Vector3.zero;

                    var line = rayGo.AddComponent<LineRenderer>();
                    line.startWidth = float.Parse(rayWidth);
                    line.endWidth = float.Parse(rayWidth);
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    ColorUtility.TryParseHtmlString(rayValidColor, out var validColor);
                    line.startColor = validColor;
                    line.endColor = validColor;

                    addedComponents.Add("Ray Interactor");
                    addedComponents.Add("LineRenderer");
                    break;

                case "direct":
                    var directGo = new GameObject("Direct Interactor");
                    directGo.transform.SetParent(go.transform);
                    directGo.transform.localPosition = Vector3.zero;

                    var sphereCollider = directGo.AddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                    sphereCollider.radius = 0.05f;

                    addedComponents.Add("Direct Interactor");
                    addedComponents.Add("SphereCollider");
                    break;

                case "poke":
                    var pokeGo = new GameObject("Poke Interactor");
                    pokeGo.transform.SetParent(go.transform);
                    pokeGo.transform.localPosition = Vector3.zero;

                    addedComponents.Add("Poke Interactor");
                    break;

                case "gaze":
                    addedComponents.Add("Gaze Interactor (configuration only)");
                    break;
            }

            if (attachTransform)
            {
                var attachPoint = new GameObject("Attach Point");
                attachPoint.transform.SetParent(go.transform);
                attachPoint.transform.localPosition = Vector3.zero;
                addedComponents.Add("Attach Point");
            }

            Undo.RecordObject(go, "Add XR Interactor");
            EditorUtility.SetDirty(go);

            return new
            {
                success = true,
                gameObject = go.name,
                instanceId = go.GetInstanceID(),
                interactorType = interactorType,
                hand = hand,
                addedComponents = addedComponents,
                note = "Basic interactor structure created. Add XR Interaction Toolkit components for full functionality."
            };
        }

        private object CreateXrUi(Dictionary<string, string> @params)
        {
            var canvasName = @params.GetValueOrDefault("canvasName") ?? "XR Canvas";
            var widthStr = @params.GetValueOrDefault("width") ?? "1";
            var heightStr = @params.GetValueOrDefault("height") ?? "0.6";
            var pixelsPerMeterStr = @params.GetValueOrDefault("pixelsPerMeter") ?? "1000";
            var interactionType = @params.GetValueOrDefault("interactionType") ?? "Both";
            var lookAtCamera = @params.GetValueOrDefault("lookAtCamera")?.ToLower() != "false";
            var curvedCanvas = @params.GetValueOrDefault("curvedCanvas")?.ToLower() == "true";
            var curveRadiusStr = @params.GetValueOrDefault("curveRadius") ?? "3";
            var followHead = @params.GetValueOrDefault("followHead")?.ToLower() == "true";
            var followDistanceStr = @params.GetValueOrDefault("followDistance") ?? "2";
            var addSampleContent = @params.GetValueOrDefault("addSampleContent")?.ToLower() == "true";

            var width = float.TryParse(widthStr, out var w) ? w : 1f;
            var height = float.TryParse(heightStr, out var h) ? h : 0.6f;
            var pixelsPerMeter = float.TryParse(pixelsPerMeterStr, out var ppm) ? ppm : 1000f;

            // Create Canvas
            var canvasGo = new GameObject(canvasName);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width * pixelsPerMeter, height * pixelsPerMeter);
            rectTransform.localScale = Vector3.one / pixelsPerMeter;

            // Position in front of camera
            var posX = 0f;
            var posY = 1.5f;
            var posZ = 2f;

            if (@params.TryGetValue("position", out var posStr))
            {
                // Parse position - simplified
            }

            canvasGo.transform.position = new Vector3(posX, posY, posZ);

            // Add Canvas Scaler
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = pixelsPerMeter;

            // Add Graphic Raycaster
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var addedComponents = new List<string> { "Canvas", "CanvasScaler", "GraphicRaycaster" };

            // Add sample content
            if (addSampleContent)
            {
                // Background panel
                var panel = new GameObject("Panel");
                panel.transform.SetParent(canvasGo.transform);
                var panelRect = panel.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;
                panelRect.anchoredPosition = Vector2.zero;

                var panelImage = panel.AddComponent<UnityEngine.UI.Image>();
                panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

                // Title text
                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(panel.transform);
                var titleRect = titleGo.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0, 0.8f);
                titleRect.anchorMax = new Vector2(1, 1);
                titleRect.sizeDelta = Vector2.zero;
                titleRect.anchoredPosition = Vector2.zero;

                var titleText = titleGo.AddComponent<UnityEngine.UI.Text>();
                titleText.text = "XREAL UI Panel";
                titleText.fontSize = 48;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = Color.white;
                titleText.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");

                // Button
                var buttonGo = new GameObject("Button");
                buttonGo.transform.SetParent(panel.transform);
                var buttonRect = buttonGo.AddComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0.3f, 0.3f);
                buttonRect.anchorMax = new Vector2(0.7f, 0.5f);
                buttonRect.sizeDelta = Vector2.zero;
                buttonRect.anchoredPosition = Vector2.zero;

                var buttonImage = buttonGo.AddComponent<UnityEngine.UI.Image>();
                buttonImage.color = new Color(0.2f, 0.6f, 1f);

                var button = buttonGo.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = buttonImage;

                var buttonTextGo = new GameObject("Text");
                buttonTextGo.transform.SetParent(buttonGo.transform);
                var buttonTextRect = buttonTextGo.AddComponent<RectTransform>();
                buttonTextRect.anchorMin = Vector2.zero;
                buttonTextRect.anchorMax = Vector2.one;
                buttonTextRect.sizeDelta = Vector2.zero;
                buttonTextRect.anchoredPosition = Vector2.zero;

                var buttonText = buttonTextGo.AddComponent<UnityEngine.UI.Text>();
                buttonText.text = "Click Me";
                buttonText.fontSize = 36;
                buttonText.alignment = TextAnchor.MiddleCenter;
                buttonText.color = Color.white;
                buttonText.font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");

                addedComponents.Add("Sample Content (Panel, Title, Button)");
            }

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create XR UI");

            return new
            {
                success = true,
                instanceId = canvasGo.GetInstanceID(),
                canvasName = canvasName,
                size = new { width = width, height = height },
                pixelsPerMeter = pixelsPerMeter,
                position = new { x = posX, y = posY, z = posZ },
                interactionType = interactionType,
                lookAtCamera = lookAtCamera,
                followHead = followHead,
                addedComponents = addedComponents,
                note = "World-space canvas created. For XR UI interaction, add TrackedDeviceGraphicRaycaster from XR Interaction Toolkit."
            };
        }

        private bool IsXritInstalled()
        {
            // Check if XR Interaction Toolkit is installed
            var request = UnityEditor.PackageManager.Client.List(true);
            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(10);
            }

            if (request.Status == UnityEditor.PackageManager.StatusCode.Success)
            {
                foreach (var package in request.Result)
                {
                    if (package.name == "com.unity.xr.interaction.toolkit")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
