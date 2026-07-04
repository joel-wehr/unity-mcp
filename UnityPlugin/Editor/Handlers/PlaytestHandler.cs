using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor;
using TMPro;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Handlers
{
    /// <summary>
    /// Autonomous playtesting handler: observe game state, simulate input,
    /// capture screenshots, and interact with game objects during play mode.
    /// </summary>
    public class PlaytestHandler : IToolHandler
    {
        public string[] SupportedMethods => new[] { "playtest" };

        public object Handle(string method, string paramsJson)
        {
            var p = JsonRpcParamsParser.ParseToDictionary(paramsJson);
            var action = p.GetValueOrDefault("action");
            if (string.IsNullOrEmpty(action))
                return new { success = false, error = "action is required" };

            switch (action.ToLower())
            {
                case "observe": return Observe();
                case "tap": return Tap(p);
                case "click_ui": return ClickUI(p);
                case "screenshot": return Screenshot(p);
                case "get_ui": return GetUI();
                case "wait_for": return WaitFor(p);
                case "interact": return Interact(p);
                case "get_grid": return GetGrid();
                case "swap_tiles": return SwapTiles(p);
                case "swipe": return Swipe(p);
                case "enter_playmode":
                    EditorApplication.isPlaying = true;
                    return new { success = true, message = "Entering play mode. Wait 2-3s then use 'observe' to confirm." };
                case "exit_playmode":
                    EditorApplication.isPlaying = false;
                    return new { success = true, message = "Exiting play mode." };
                default: return new { success = false, error = $"Unknown action: {action}" };
            }
        }

        // ===== OBSERVE =====
        // Returns a full snapshot of the current game state
        private object Observe()
        {
            var scene = SceneManager.GetActiveScene();
            var isPlaying = EditorApplication.isPlaying;
            var isPaused = EditorApplication.isPaused;

            // Collect all visible TMP text elements
            var uiTexts = new List<object>();
            if (isPlaying)
            {
                var tmpTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
                foreach (var t in tmpTexts)
                {
                    if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
                    uiTexts.Add(new
                    {
                        name = t.gameObject.name,
                        text = t.text,
                        parent = t.transform.parent?.name ?? "root"
                    });
                }
            }

            // Collect active buttons
            var buttons = new List<object>();
            if (isPlaying)
            {
                var allButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
                foreach (var btn in allButtons)
                {
                    if (!btn.gameObject.activeInHierarchy) continue;
                    var label = btn.GetComponentInChildren<TMP_Text>();
                    buttons.Add(new
                    {
                        name = btn.gameObject.name,
                        interactable = btn.interactable,
                        label = label?.text ?? ""
                    });
                }
            }

            // Camera info
            var cam = Camera.main;
            object cameraInfo = null;
            if (cam != null)
            {
                cameraInfo = new
                {
                    position = new { x = cam.transform.position.x, y = cam.transform.position.y, z = cam.transform.position.z },
                    orthographic = cam.orthographic,
                    orthographicSize = cam.orthographicSize,
                    screenWidth = Screen.width,
                    screenHeight = Screen.height
                };
            }

            return new
            {
                success = true,
                isPlaying,
                isPaused,
                activeScene = scene.name,
                sceneIndex = scene.buildIndex,
                time = isPlaying ? Time.time : 0f,
                frame = isPlaying ? Time.frameCount : 0,
                timeScale = Time.timeScale,
                gameObjectCount = isPlaying ? SceneManager.GetActiveScene().rootCount : 0,
                camera = cameraInfo,
                uiTexts,
                buttons
            };
        }

        // ===== TAP =====
        // Simulate a tap at screen coordinates - tries UI, then Physics2D, then Physics3D
        private object Tap(Dictionary<string, string> p)
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode to tap" };

            if (!float.TryParse(p.GetValueOrDefault("x"), out var x) ||
                !float.TryParse(p.GetValueOrDefault("y"), out var y))
                return new { success = false, error = "x and y screen coordinates are required" };

            var screenPos = new Vector2(x, y);

            // 1. Try EventSystem raycast (UI elements)
            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                var pointerData = new PointerEventData(eventSystem) { position = screenPos };
                var results = new List<RaycastResult>();
                eventSystem.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    var target = results[0].gameObject;
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);

                    return new
                    {
                        success = true,
                        hitType = "ui",
                        hitObject = target.name,
                        hitPath = GetPath(target),
                        screenPosition = new { x, y }
                    };
                }
            }

            // 2. Try Physics2D
            var cam = Camera.main;
            if (cam != null)
            {
                var worldPos = cam.ScreenToWorldPoint(new Vector3(x, y, -cam.transform.position.z));
                var collider2D = Physics2D.OverlapPoint(new Vector2(worldPos.x, worldPos.y));

                if (collider2D != null)
                {
                    var target = collider2D.gameObject;

                    // Try IPointerClickHandler
                    var pointerData = new PointerEventData(eventSystem ?? EventSystem.current) { position = screenPos };
                    if (!ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler))
                    {
                        // Fallback: SendMessage
                        target.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);
                        target.SendMessage("OnMouseUpAsButton", SendMessageOptions.DontRequireReceiver);
                    }

                    return new
                    {
                        success = true,
                        hitType = "physics2d",
                        hitObject = target.name,
                        hitPath = GetPath(target),
                        worldPosition = new { x = worldPos.x, y = worldPos.y },
                        screenPosition = new { x, y }
                    };
                }

                // 3. Try Physics3D
                var ray = cam.ScreenPointToRay(new Vector3(x, y, 0));
                if (Physics.Raycast(ray, out var hit3D))
                {
                    var target = hit3D.collider.gameObject;
                    target.SendMessage("OnMouseDown", SendMessageOptions.DontRequireReceiver);
                    target.SendMessage("OnMouseUpAsButton", SendMessageOptions.DontRequireReceiver);

                    return new
                    {
                        success = true,
                        hitType = "physics3d",
                        hitObject = target.name,
                        hitPath = GetPath(target),
                        worldPosition = new { x = hit3D.point.x, y = hit3D.point.y, z = hit3D.point.z },
                        screenPosition = new { x, y }
                    };
                }
            }

            return new
            {
                success = true,
                hitType = "none",
                message = $"No object found at screen position ({x}, {y})",
                screenPosition = new { x, y }
            };
        }

        // ===== CLICK_UI =====
        // Click a UI element by name (deep search across all canvases)
        private object ClickUI(Dictionary<string, string> p)
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            var elementName = p.GetValueOrDefault("name");
            if (string.IsNullOrEmpty(elementName))
                return new { success = false, error = "name is required (UI element name to click)" };

            // Search all canvases
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                var target = FindDeep(canvas.transform, elementName);
                if (target == null || !target.gameObject.activeInHierarchy) continue;

                // Try Button
                var button = target.GetComponent<Button>();
                if (button != null)
                {
                    if (!button.interactable)
                        return new { success = false, error = $"Button '{elementName}' is not interactable" };

                    button.onClick.Invoke();
                    return new
                    {
                        success = true,
                        clicked = elementName,
                        path = GetPath(target.gameObject),
                        method = "Button.onClick"
                    };
                }

                // Try Toggle
                var toggle = target.GetComponent<Toggle>();
                if (toggle != null)
                {
                    if (!toggle.interactable)
                        return new { success = false, error = $"Toggle '{elementName}' is not interactable" };

                    toggle.isOn = !toggle.isOn;
                    return new
                    {
                        success = true,
                        clicked = elementName,
                        path = GetPath(target.gameObject),
                        method = "Toggle.isOn",
                        newValue = toggle.isOn
                    };
                }

                // Try Slider (set to specific value if provided)
                var slider = target.GetComponent<Slider>();
                if (slider != null)
                {
                    if (p.TryGetValue("value", out var valStr) && float.TryParse(valStr, out var val))
                    {
                        slider.value = val;
                        return new
                        {
                            success = true,
                            clicked = elementName,
                            method = "Slider.value",
                            newValue = val
                        };
                    }
                }

                // Try TMP_InputField (set text if provided)
                var inputField = target.GetComponent<TMP_InputField>();
                if (inputField != null)
                {
                    var inputText = p.GetValueOrDefault("text") ?? "";
                    inputField.text = inputText;
                    inputField.onEndEdit?.Invoke(inputText);
                    return new
                    {
                        success = true,
                        clicked = elementName,
                        method = "TMP_InputField.text",
                        newText = inputText
                    };
                }

                // Fallback: ExecuteEvents pointer click
                var pointerData = new PointerEventData(EventSystem.current);
                if (ExecuteEvents.Execute(target.gameObject, pointerData, ExecuteEvents.pointerClickHandler))
                {
                    return new
                    {
                        success = true,
                        clicked = elementName,
                        method = "ExecuteEvents.pointerClick"
                    };
                }

                return new { success = false, error = $"Found '{elementName}' but it has no clickable/interactive component" };
            }

            // Element not found — provide helpful suggestions
            var availableButtons = new List<string>();
            foreach (var canvas in canvases)
            {
                var allButtons = canvas.GetComponentsInChildren<Button>(false);
                foreach (var btn in allButtons)
                {
                    if (btn.gameObject.activeInHierarchy)
                        availableButtons.Add(btn.gameObject.name);
                }
            }

            return new
            {
                success = false,
                error = $"UI element '{elementName}' not found",
                availableButtons = availableButtons.Count > 0 ? availableButtons : null
            };
        }

        // ===== SCREENSHOT =====
        // Capture the camera view and save to a PNG file
        private object Screenshot(Dictionary<string, string> p)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
                if (cam == null)
                    return new { success = false, error = "No camera found in scene" };
            }

            int width = 540;
            int height = 960;
            if (p.TryGetValue("width", out var ws) && int.TryParse(ws, out var w)) width = w;
            if (p.TryGetValue("height", out var hs) && int.TryParse(hs, out var h)) height = h;

            // Render camera to RenderTexture
            var rt = RenderTexture.GetTemporary(width, height, 24);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();

            // Read pixels into Texture2D
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Restore camera
            cam.targetTexture = prevTarget;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            // Encode to PNG
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            // Save to temp file
            var screenshotDir = Path.Combine(Application.temporaryCachePath, "PlaytestScreenshots");
            if (!Directory.Exists(screenshotDir))
                Directory.CreateDirectory(screenshotDir);

            var filename = $"playtest_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var path = Path.Combine(screenshotDir, filename);
            File.WriteAllBytes(path, png);

            return new
            {
                success = true,
                path,
                width,
                height,
                sizeBytes = png.Length,
                note = "Camera render (no Screen Space Overlay UI). Use 'get_ui' action for UI state."
            };
        }

        // ===== GET_UI =====
        // Enumerate all active UI elements with their types, text, and state
        private object GetUI()
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            var elements = new List<object>();
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (var canvas in canvases)
            {
                if (!canvas.gameObject.activeInHierarchy) continue;
                CollectUIElements(canvas.transform, elements);
            }

            return new
            {
                success = true,
                elementCount = elements.Count,
                elements
            };
        }

        private void CollectUIElements(Transform t, List<object> elements)
        {
            if (!t.gameObject.activeInHierarchy) return;

            string type = null;
            var info = new Dictionary<string, object>
            {
                ["name"] = t.name,
                ["path"] = GetPath(t.gameObject)
            };

            // Check for interactive elements (most specific first)
            var button = t.GetComponent<Button>();
            if (button != null)
            {
                type = "button";
                info["interactable"] = button.interactable;
                var label = t.GetComponentInChildren<TMP_Text>();
                if (label != null) info["label"] = label.text;
            }

            var toggle = t.GetComponent<Toggle>();
            if (toggle != null)
            {
                type = "toggle";
                info["isOn"] = toggle.isOn;
                info["interactable"] = toggle.interactable;
            }

            var slider = t.GetComponent<Slider>();
            if (slider != null)
            {
                type = "slider";
                info["value"] = slider.value;
                info["min"] = slider.minValue;
                info["max"] = slider.maxValue;
                info["interactable"] = slider.interactable;
            }

            var inputField = t.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                type = "input";
                info["text"] = inputField.text;
                info["interactable"] = inputField.interactable;
            }

            // Check for text (if not already captured via button label)
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null && type == null)
            {
                if (!string.IsNullOrEmpty(tmp.text))
                {
                    type = "text";
                    info["text"] = tmp.text;
                    info["fontSize"] = tmp.fontSize;
                }
            }

            // Check for image (only standalone images, not part of buttons)
            var image = t.GetComponent<Image>();
            if (image != null && type == null)
            {
                // Skip images that are just backgrounds or parts of other controls
                if (t.GetComponent<Button>() == null && t.GetComponent<Toggle>() == null &&
                    t.GetComponent<Slider>() == null && t.childCount == 0)
                {
                    type = "image";
                }
            }

            if (type != null)
            {
                info["type"] = type;
                elements.Add(info);
            }

            // Recurse
            for (int i = 0; i < t.childCount; i++)
                CollectUIElements(t.GetChild(i), elements);
        }

        // ===== WAIT_FOR =====
        // Check if a condition is currently met (caller polls)
        private object WaitFor(Dictionary<string, string> p)
        {
            var condition = p.GetValueOrDefault("condition");
            if (string.IsNullOrEmpty(condition))
                return new { success = false, error = "condition is required: scene, text, object, playing, paused" };

            switch (condition.ToLower())
            {
                case "scene":
                {
                    var sceneName = p.GetValueOrDefault("sceneName");
                    if (string.IsNullOrEmpty(sceneName))
                        return new { success = false, error = "sceneName is required" };
                    var current = SceneManager.GetActiveScene().name;
                    return new { success = true, met = current == sceneName, currentScene = current, expectedScene = sceneName };
                }

                case "text":
                {
                    var searchText = p.GetValueOrDefault("text");
                    if (string.IsNullOrEmpty(searchText))
                        return new { success = false, error = "text is required" };

                    if (!EditorApplication.isPlaying)
                        return new { success = true, met = false, reason = "not in play mode" };

                    var tmpTexts = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
                    foreach (var t in tmpTexts)
                    {
                        if (t.gameObject.activeInHierarchy &&
                            t.text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            return new
                            {
                                success = true,
                                met = true,
                                searchText,
                                foundIn = t.gameObject.name,
                                fullText = t.text
                            };
                        }
                    }
                    return new { success = true, met = false, searchText, foundIn = (string)null };
                }

                case "object":
                {
                    var objectName = p.GetValueOrDefault("objectName");
                    if (string.IsNullOrEmpty(objectName))
                        return new { success = false, error = "objectName is required" };
                    var obj = GameObject.Find(objectName);
                    return new
                    {
                        success = true,
                        met = obj != null && obj.activeInHierarchy,
                        objectName,
                        exists = obj != null,
                        active = obj?.activeInHierarchy ?? false
                    };
                }

                case "playing":
                    return new { success = true, met = EditorApplication.isPlaying };

                case "paused":
                    return new { success = true, met = EditorApplication.isPaused };

                default:
                    return new { success = false, error = $"Unknown condition: {condition}. Valid: scene, text, object, playing, paused" };
            }
        }

        // ===== INTERACT =====
        // Call a method on a game object's component via reflection
        private object Interact(Dictionary<string, string> p)
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            var objectPath = p.GetValueOrDefault("objectPath");
            var componentType = p.GetValueOrDefault("componentType");
            var methodName = p.GetValueOrDefault("methodName");

            if (string.IsNullOrEmpty(objectPath))
                return new { success = false, error = "objectPath is required" };
            if (string.IsNullOrEmpty(methodName))
                return new { success = false, error = "methodName is required" };

            var go = GameObject.Find(objectPath);
            if (go == null)
                return new { success = false, error = $"GameObject '{objectPath}' not found" };

            // Find the target component or use the GameObject itself
            object target;
            if (!string.IsNullOrEmpty(componentType))
            {
                Component comp = null;
                // Search all components for a type name match
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var typeName = c.GetType().Name;
                    var fullName = c.GetType().FullName;
                    if (typeName == componentType || fullName == componentType)
                    {
                        comp = c;
                        break;
                    }
                }
                if (comp == null)
                    return new { success = false, error = $"Component '{componentType}' not found on '{objectPath}'" };
                target = comp;
            }
            else
            {
                target = go;
            }

            // Find the method
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                return new { success = false, error = $"Method '{methodName}' not found on {target.GetType().Name}" };

            // Parse arguments if provided
            var argsStr = p.GetValueOrDefault("args");
            object[] args = null;
            if (!string.IsNullOrEmpty(argsStr))
            {
                var paramInfos = method.GetParameters();
                var argParts = argsStr.Split(',');
                args = new object[paramInfos.Length];
                for (int i = 0; i < paramInfos.Length && i < argParts.Length; i++)
                {
                    args[i] = ConvertArg(argParts[i].Trim(), paramInfos[i].ParameterType);
                }
            }

            try
            {
                var result = method.Invoke(target, args);
                return new
                {
                    success = true,
                    objectPath,
                    component = target.GetType().Name,
                    methodName,
                    result = result?.ToString() ?? "void",
                    resultType = result?.GetType().Name ?? "void"
                };
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return new { success = false, error = $"Method '{methodName}' threw: {inner.Message}" };
            }
        }

        // ===== GET_GRID =====
        // Returns the full tile grid state for a match-3 style game
        private object GetGrid()
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            var allMBs = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMBs)
            {
                if (mb == null) continue;
                var gridProp = mb.GetType().GetProperty("Grid");
                var colsProp = mb.GetType().GetProperty("Columns");
                var rowsProp = mb.GetType().GetProperty("Rows");
                if (gridProp == null || colsProp == null || rowsProp == null) continue;

                int cols = (int)colsProp.GetValue(mb);
                int rows = (int)rowsProp.GetValue(mb);
                var grid = gridProp.GetValue(mb) as Array;
                if (grid == null) continue;

                var tiles = new List<object>();
                for (int col = 0; col < cols; col++)
                {
                    for (int row = 0; row < rows; row++)
                    {
                        var tile = grid.GetValue(col, row) as MonoBehaviour;
                        if (tile == null)
                        {
                            tiles.Add(new { col, row, empty = true });
                            continue;
                        }

                        var freqProp = tile.GetType().GetProperty("Frequency");
                        var isStaticProp = tile.GetType().GetProperty("IsStatic");
                        var isMatchedProp = tile.GetType().GetProperty("IsMatched");

                        tiles.Add(new
                        {
                            col, row,
                            empty = false,
                            frequency = freqProp?.GetValue(tile)?.ToString() ?? "unknown",
                            isStatic = (bool)(isStaticProp?.GetValue(tile) ?? false),
                            isMatched = (bool)(isMatchedProp?.GetValue(tile) ?? false)
                        });
                    }
                }

                var processingProp = mb.GetType().GetProperty("IsProcessing");
                bool isProcessing = (bool)(processingProp?.GetValue(mb) ?? false);

                return new { success = true, columns = cols, rows = rows, isProcessing, tiles };
            }

            return new { success = false, error = "No grid component found in scene" };
        }

        // ===== SWAP_TILES =====
        // Directly invoke TrySwapTiles on the grid processor by grid coordinates
        private object SwapTiles(Dictionary<string, string> p)
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            if (!int.TryParse(p.GetValueOrDefault("col1"), out var col1) ||
                !int.TryParse(p.GetValueOrDefault("row1"), out var row1) ||
                !int.TryParse(p.GetValueOrDefault("col2"), out var col2) ||
                !int.TryParse(p.GetValueOrDefault("row2"), out var row2))
                return new { success = false, error = "col1, row1, col2, row2 grid coordinates are required" };

            var allMBs = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMBs)
            {
                if (mb == null) continue;
                var trySwap = mb.GetType().GetMethod("TrySwapTiles", BindingFlags.Public | BindingFlags.Instance);
                if (trySwap == null) continue;

                // Check processing state
                var processingProp = mb.GetType().GetProperty("IsProcessing");
                if (processingProp != null && (bool)processingProp.GetValue(mb))
                    return new { success = false, error = "Grid is processing a cascade. Wait and retry." };

                var posA = new Vector2Int(col1, row1);
                var posB = new Vector2Int(col2, row2);

                // Validate positions
                var isValidMethod = mb.GetType().GetMethod("IsValidPosition", BindingFlags.Public | BindingFlags.Instance);
                if (isValidMethod != null)
                {
                    if (!(bool)isValidMethod.Invoke(mb, new object[] { posA }))
                        return new { success = false, error = $"Position ({col1},{row1}) is out of bounds" };
                    if (!(bool)isValidMethod.Invoke(mb, new object[] { posB }))
                        return new { success = false, error = $"Position ({col2},{row2}) is out of bounds" };
                }

                // Get tile info before swap
                var getTile = mb.GetType().GetMethod("GetTileAt", BindingFlags.Public | BindingFlags.Instance);
                string freqA = "?", freqB = "?";
                if (getTile != null)
                {
                    var tileA = getTile.Invoke(mb, new object[] { posA }) as MonoBehaviour;
                    var tileB = getTile.Invoke(mb, new object[] { posB }) as MonoBehaviour;

                    if (tileA == null) return new { success = false, error = $"No tile at ({col1},{row1})" };
                    if (tileB == null) return new { success = false, error = $"No tile at ({col2},{row2})" };

                    freqA = tileA.GetType().GetProperty("Frequency")?.GetValue(tileA)?.ToString() ?? "?";
                    freqB = tileB.GetType().GetProperty("Frequency")?.GetValue(tileB)?.ToString() ?? "?";

                    bool staticA = (bool)(tileA.GetType().GetProperty("IsStatic")?.GetValue(tileA) ?? false);
                    bool staticB = (bool)(tileB.GetType().GetProperty("IsStatic")?.GetValue(tileB) ?? false);
                    if (staticA) return new { success = false, error = $"Tile at ({col1},{row1}) is static" };
                    if (staticB) return new { success = false, error = $"Tile at ({col2},{row2}) is static" };
                }

                // Start the swap coroutine
                var coroutine = trySwap.Invoke(mb, new object[] { posA, posB });
                mb.StartCoroutine((System.Collections.IEnumerator)coroutine);

                return new
                {
                    success = true,
                    from = new { col = col1, row = row1, frequency = freqA },
                    to = new { col = col2, row = row2, frequency = freqB },
                    note = "Swap started. Use 'observe' or 'get_grid' after ~0.5s to check results."
                };
            }

            return new { success = false, error = "No grid component with TrySwapTiles found in scene" };
        }

        // ===== SWIPE =====
        // Simulate a swipe gesture by invoking HandleTouch with Began/Moved/Ended phases
        private object Swipe(Dictionary<string, string> p)
        {
            if (!EditorApplication.isPlaying)
                return new { success = false, error = "Must be in play mode" };

            if (!float.TryParse(p.GetValueOrDefault("start_x"), out var sx) ||
                !float.TryParse(p.GetValueOrDefault("start_y"), out var sy) ||
                !float.TryParse(p.GetValueOrDefault("end_x"), out var ex) ||
                !float.TryParse(p.GetValueOrDefault("end_y"), out var ey))
                return new { success = false, error = "start_x, start_y, end_x, end_y screen coordinates required" };

            var allMBs = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allMBs)
            {
                if (mb == null) continue;
                var method = mb.GetType().GetMethod("HandleTouch",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(TouchPhase), typeof(Vector2) },
                    null);
                if (method == null) continue;

                var processingProp = mb.GetType().GetProperty("IsProcessing");
                if (processingProp != null && (bool)processingProp.GetValue(mb))
                    return new { success = false, error = "Grid is processing a cascade. Wait and retry." };

                // Simulate touch sequence
                method.Invoke(mb, new object[] { TouchPhase.Began, new Vector2(sx, sy) });
                method.Invoke(mb, new object[] { TouchPhase.Moved, new Vector2(ex, ey) });
                method.Invoke(mb, new object[] { TouchPhase.Ended, new Vector2(ex, ey) });

                float dx = ex - sx, dy = ey - sy;
                return new
                {
                    success = true,
                    from = new { x = sx, y = sy },
                    to = new { x = ex, y = ey },
                    delta = new { x = dx, y = dy, magnitude = Mathf.Sqrt(dx * dx + dy * dy) },
                    note = "Swipe simulated. Delta must exceed 30px for swap. Check 'get_grid' after ~0.5s."
                };
            }

            return new { success = false, error = "No component with HandleTouch method found" };
        }

        #region Helpers

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindDeep(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private static string GetPath(GameObject go)
        {
            var parts = new List<string>();
            var t = go.transform;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static object ConvertArg(string value, Type targetType)
        {
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.TryParse(value, out var i) ? i : 0;
            if (targetType == typeof(float)) return float.TryParse(value, out var f) ? f : 0f;
            if (targetType == typeof(bool)) return value.ToLower() == "true";
            if (targetType == typeof(double)) return double.TryParse(value, out var d) ? d : 0.0;
            if (targetType == typeof(long)) return long.TryParse(value, out var l) ? l : 0L;
            return value;
        }

        #endregion
    }
}
