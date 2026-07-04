using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Main MCP Unity Bridge that manages the WebSocket server and routes tool requests.
    /// This is the central hub for all MCP communication with Unity.
    /// </summary>
    [InitializeOnLoad]
    public static class McpUnityBridge
    {
        private static McpWebSocketServer _server;
        private static readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private static readonly object _queueLock = new object();
        private static readonly Dictionary<string, IToolHandler> _toolHandlers = new Dictionary<string, IToolHandler>();
        private static readonly Dictionary<string, IResourceHandler> _resourceHandlers = new Dictionary<string, IResourceHandler>();

        public static bool IsRunning => _server?.IsRunning ?? false;
        public static int ClientCount => _server?.ClientCount ?? 0;
        public static int Port => McpSettings.Instance.Port;

        static McpUnityBridge()
        {
            EditorApplication.update += ProcessMainThreadQueue;
            EditorApplication.quitting += OnEditorQuitting;
        }

        [InitializeOnLoadMethod]
        private static void OnEditorLoad()
        {
            Debug.Log("[MCP] InitializeOnLoadMethod triggered");
            try
            {
                // Use EditorApplication.update to start after editor is fully loaded
                void DelayedStart()
                {
                    EditorApplication.update -= DelayedStart;
                    Debug.Log("[MCP] Starting server from update callback...");
                    Start();
                }
                EditorApplication.update += DelayedStart;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Error in OnEditorLoad: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public static void Start()
        {
            if (_server != null && _server.IsRunning)
            {
                Debug.Log("[MCP] Server already running");
                return;
            }

            RegisterHandlers();

            _server = new McpWebSocketServer(McpSettings.Instance.Port);
            _server.OnMessageReceived += HandleMessage;
            _server.OnClientConnected += OnClientConnected;
            _server.OnClientDisconnected += OnClientDisconnected;
            _server.Start();

            SaveSettings();
        }

        public static void Stop()
        {
            _server?.Stop();
            _server = null;
            Debug.Log("[MCP] Server stopped");
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        public static void EnqueueMainThread(Action action)
        {
            lock (_queueLock)
            {
                _mainThreadQueue.Enqueue(action);
            }
        }

        private static void ProcessMainThreadQueue()
        {
            // Only process if there's actually something in the queue
            if (_mainThreadQueue.Count == 0) return;

            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                {
                    try
                    {
                        var action = _mainThreadQueue.Dequeue();
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MCP] Error processing main thread action: {ex.Message}");
                    }
                }
            }
        }

        private static void RegisterHandlers()
        {
            _toolHandlers.Clear();
            _resourceHandlers.Clear();

            // Register core Unity tool handlers
            RegisterToolHandler(new Handlers.GameObjectHandler());
            RegisterToolHandler(new Handlers.SceneHandler());
            RegisterToolHandler(new Handlers.ComponentHandler());
            RegisterToolHandler(new Handlers.AssetHandler());
            RegisterToolHandler(new Handlers.EditorHandler());
            RegisterToolHandler(new Handlers.TestHandler());
            RegisterToolHandler(new Handlers.ConsoleHandler());
            RegisterToolHandler(new Handlers.BuildHandler());
            RegisterToolHandler(new Handlers.AssetStoreHandler());

            // Register advanced tool handlers
            RegisterToolHandler(new Handlers.CodeExecutionHandler());
            RegisterToolHandler(new Handlers.EditorControlHandler());
            RegisterToolHandler(new Handlers.ProjectSettingsHandler());
            RegisterToolHandler(new Handlers.ScriptManagementHandler());
            RegisterToolHandler(new Handlers.UndoRedoHandler());
            RegisterToolHandler(new Handlers.DebuggerHandler());
            RegisterToolHandler(new Handlers.AssetImportHandler());
            RegisterToolHandler(new Handlers.WatchConsoleHandler());
            RegisterToolHandler(new Handlers.ProfilerHandler());
            RegisterToolHandler(new Handlers.AnimationHandler());
            RegisterToolHandler(new Handlers.PhysicsHandler());
            RegisterToolHandler(new Handlers.MaterialShaderHandler());
            RegisterToolHandler(new Handlers.LightingHandler());
            RegisterToolHandler(new Handlers.FileHandler());
            RegisterToolHandler(new Handlers.ScriptableObjectHandler());
            RegisterToolHandler(new Handlers.PrefabHandler());
            RegisterToolHandler(new Handlers.AudioMixerHandler());
            RegisterToolHandler(new Handlers.TerrainHandler());
            RegisterToolHandler(new Handlers.NavMeshHandler());
            RegisterToolHandler(new Handlers.Physics2DHandler());
            RegisterToolHandler(new Handlers.TilemapHandler());
            RegisterToolHandler(new Handlers.SpriteHandler());
            RegisterToolHandler(new Handlers.ParticleSystemHandler());
            RegisterToolHandler(new Handlers.PlaytestHandler());
            RegisterToolHandler(new Handlers.ResourceBridgeHandler());

            // Register XREAL tool handlers
            RegisterToolHandler(new Handlers.Xreal.XrealProjectHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealDeviceHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealHandTrackingHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealSpatialMappingHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealImageTrackingHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealMixedRealityHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealBuildHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealXrInteractionHandler());
            RegisterToolHandler(new Handlers.Xreal.XrealPerformanceHandler());

            // Register resource handlers
            RegisterResourceHandler(new Handlers.Resources.SceneHierarchyResource());
            RegisterResourceHandler(new Handlers.Resources.GameObjectResource());
            RegisterResourceHandler(new Handlers.Resources.ConsoleLogsResource());
            RegisterResourceHandler(new Handlers.Resources.AssetsResource());
            RegisterResourceHandler(new Handlers.Resources.PackagesResource());

            // Register XREAL resource handlers
            RegisterResourceHandler(new Handlers.Resources.Xreal.DeviceStateResource());
            RegisterResourceHandler(new Handlers.Resources.Xreal.HandTrackingResource());
            RegisterResourceHandler(new Handlers.Resources.Xreal.SpatialAnchorsResource());
            RegisterResourceHandler(new Handlers.Resources.Xreal.DetectedPlanesResource());
            RegisterResourceHandler(new Handlers.Resources.Xreal.TrackedImagesResource());
            RegisterResourceHandler(new Handlers.Resources.Xreal.BuildSettingsResource());

            Debug.Log($"[MCP] Registered {_toolHandlers.Count} tool handlers and {_resourceHandlers.Count} resource handlers");
        }

        public static void RegisterToolHandler(IToolHandler handler)
        {
            foreach (var method in handler.SupportedMethods)
            {
                _toolHandlers[method] = handler;
            }
        }

        public static void RegisterResourceHandler(IResourceHandler handler)
        {
            foreach (var uri in handler.SupportedUris)
            {
                _resourceHandlers[uri] = handler;
            }
        }

        private static void HandleMessage(McpClientConnection client, string message)
        {
            try
            {
                var request = JsonUtility.FromJson<JsonRpcRequest>(message);

                // Extract raw params JSON for proper parsing
                var paramsJson = JsonRpcParamsParser.ExtractParamsJson(message);

                // Handle resource requests
                if (request.method.StartsWith("resource/"))
                {
                    HandleResourceRequest(client, request, paramsJson);
                    return;
                }

                // Handle tool requests
                if (_toolHandlers.TryGetValue(request.method, out var handler))
                {
                    var result = handler.Handle(request.method, paramsJson);
                    SendResponse(client, request.id, result);
                }
                else
                {
                    SendError(client, request.id, "METHOD_NOT_FOUND", $"Unknown method: {request.method}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCP] Error handling message: {ex.Message}\n{ex.StackTrace}");
                try
                {
                    var request = JsonUtility.FromJson<JsonRpcRequest>(message);
                    SendError(client, request.id, "INTERNAL_ERROR", ex.Message);
                }
                catch
                {
                    // Can't even parse the request ID
                }
            }
        }

        private static void HandleResourceRequest(McpClientConnection client, JsonRpcRequest request, string paramsJson)
        {
            var uri = JsonRpcParamsParser.ExtractUri(paramsJson);

            // Find matching resource handler
            foreach (var kvp in _resourceHandlers)
            {
                if (uri.StartsWith(kvp.Key) || MatchesUriPattern(uri, kvp.Key))
                {
                    var result = kvp.Value.Handle(uri, paramsJson);
                    SendResponse(client, request.id, result);
                    return;
                }
            }

            SendError(client, request.id, "RESOURCE_NOT_FOUND", $"Unknown resource: {uri}");
        }

        private static bool MatchesUriPattern(string uri, string pattern)
        {
            // Handle patterns like "unity://gameobject/{id}"
            var patternParts = pattern.Split('/');
            var uriParts = uri.Split('/');

            if (patternParts.Length != uriParts.Length) return false;

            for (int i = 0; i < patternParts.Length; i++)
            {
                if (patternParts[i].StartsWith("{") && patternParts[i].EndsWith("}"))
                {
                    continue; // Wildcard match
                }
                if (patternParts[i] != uriParts[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void SendResponse(McpClientConnection client, string id, object result)
        {
            // Use simple JSON construction since JsonUtility doesn't handle anonymous objects
            var resultJson = SerializeToJson(result);
            var json = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{id}\",\"result\":{resultJson}}}";
            client.Send(json);
        }

        private static string SerializeToJson(object obj)
        {
            if (obj == null) return "null";

            var type = obj.GetType();

            // Handle primitives
            if (obj is bool b) return b ? "true" : "false";
            if (obj is string s) return $"\"{EscapeJson(s)}\"";
            if (obj is int || obj is long || obj is float || obj is double) return obj.ToString();

            // Handle collections
            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var items = new System.Collections.Generic.List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(SerializeToJson(item));
                }
                return "[" + string.Join(",", items) + "]";
            }

            // Handle anonymous types and objects via reflection
            var props = type.GetProperties();
            var fields = type.GetFields();
            var members = new System.Collections.Generic.List<string>();

            foreach (var prop in props)
            {
                if (prop.CanRead)
                {
                    var value = prop.GetValue(obj);
                    members.Add($"\"{prop.Name}\":{SerializeToJson(value)}");
                }
            }

            foreach (var field in fields)
            {
                if (field.IsPublic)
                {
                    var value = field.GetValue(obj);
                    members.Add($"\"{field.Name}\":{SerializeToJson(value)}");
                }
            }

            return "{" + string.Join(",", members) + "}";
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private static void SendError(McpClientConnection client, string id, string errorType, string message, object details = null)
        {
            var detailsJson = details != null ? SerializeToJson(details) : "null";
            var json = $"{{\"jsonrpc\":\"2.0\",\"id\":\"{id}\",\"error\":{{\"type\":\"{EscapeJson(errorType)}\",\"message\":\"{EscapeJson(message)}\",\"details\":{detailsJson}}}}}";
            client.Send(json);
        }

        private static void OnClientConnected(McpClientConnection client)
        {
            Debug.Log($"[MCP] Client connected: {client.Name}");
        }

        private static void OnClientDisconnected(McpClientConnection client)
        {
            Debug.Log($"[MCP] Client disconnected: {client.Name}");
        }

        private static void OnEditorQuitting()
        {
            Stop();
        }

        private static void SaveSettings()
        {
            // Save settings to ProjectSettings folder for the MCP server to read
            var settingsPath = System.IO.Path.Combine(Application.dataPath, "../ProjectSettings/McpUnitySettings.json");
            var settings = new McpUnitySettingsFile
            {
                Port = McpSettings.Instance.Port,
                Host = "localhost",
                RequestTimeoutSeconds = McpSettings.Instance.RequestTimeout
            };

            var json = JsonUtility.ToJson(settings, true);
            System.IO.File.WriteAllText(settingsPath, json);
        }

        [Serializable]
        private class McpUnitySettingsFile
        {
            public int Port;
            public string Host;
            public int RequestTimeoutSeconds;
        }
    }

    // JSON-RPC message types
    [Serializable]
    public class JsonRpcRequest
    {
        public string jsonrpc;
        public string id;
        public string method;
        // Note: params is parsed separately as raw JSON due to JsonUtility limitations
    }

    /// <summary>
    /// Helper class for parsing JSON-RPC params from raw JSON.
    /// Unity's JsonUtility doesn't support dynamic objects, so we parse manually.
    /// </summary>
    public static class JsonRpcParamsParser
    {
        /// <summary>
        /// Extracts the "params" object from a JSON-RPC message as a raw JSON string.
        /// </summary>
        public static string ExtractParamsJson(string fullMessage)
        {
            // Find "params": in the message
            var paramsKey = "\"params\":";
            var paramsIndex = fullMessage.IndexOf(paramsKey);
            if (paramsIndex < 0) return "{}";

            var startIndex = paramsIndex + paramsKey.Length;

            // Skip whitespace
            while (startIndex < fullMessage.Length && char.IsWhiteSpace(fullMessage[startIndex]))
                startIndex++;

            if (startIndex >= fullMessage.Length) return "{}";

            // Handle null params
            if (fullMessage.Substring(startIndex).StartsWith("null"))
                return "{}";

            // Find the matching closing brace
            if (fullMessage[startIndex] != '{') return "{}";

            var braceCount = 0;
            var endIndex = startIndex;
            var inString = false;
            var escaped = false;

            for (var i = startIndex; i < fullMessage.Length; i++)
            {
                var c = fullMessage[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString) continue;

                if (c == '{') braceCount++;
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }
            }

            return fullMessage.Substring(startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        /// Parses a JSON object into a Dictionary of string key-value pairs.
        /// Handles nested objects by keeping them as JSON strings.
        /// </summary>
        public static Dictionary<string, string> ParseToDictionary(string json)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(json) || json == "{}" || json == "null") return dict;

            // Remove outer braces
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            // Parse key-value pairs
            var i = 0;
            while (i < json.Length)
            {
                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                if (i >= json.Length) break;

                // Parse key
                if (json[i] != '"') { i++; continue; }
                i++; // Skip opening quote
                var keyStart = i;
                while (i < json.Length && json[i] != '"') i++;
                var key = json.Substring(keyStart, i - keyStart);
                i++; // Skip closing quote

                // Skip to colon
                while (i < json.Length && json[i] != ':') i++;
                i++; // Skip colon

                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                // Parse value
                string value;
                if (i >= json.Length)
                {
                    value = "";
                }
                else if (json[i] == '"')
                {
                    // String value — JSON-unescape in a single forward pass.
                    //
                    // The previous implementation used a chain of String.Replace calls
                    // (\\" → ", \\\\ → \, \\n → \n, \\r → \r, \\t → \t). That double-decodes:
                    // after \\\\→\\ collapses, the next pass mistook a USER backslash+n for
                    // a JSON escape and converted it to a real newline. The visible symptom
                    // was CS1010 "Newline in constant" when execute_code callers passed a C#
                    // source containing the two-char sequence \n inside a string literal.
                    //
                    // Single-pass unescape eliminates the ambiguity by consuming each escape
                    // exactly once, so \\n on the wire becomes the two characters \ and n
                    // in the resulting string, and \n on the wire becomes a real newline.
                    i++; // Skip opening quote
                    var sb = new System.Text.StringBuilder();
                    while (i < json.Length)
                    {
                        var ch = json[i];
                        if (ch == '\\')
                        {
                            if (i + 1 >= json.Length) { i++; break; }
                            var esc = json[i + 1];
                            switch (esc)
                            {
                                case '"': sb.Append('"'); i += 2; break;
                                case '\\': sb.Append('\\'); i += 2; break;
                                case '/': sb.Append('/'); i += 2; break;
                                case 'n': sb.Append('\n'); i += 2; break;
                                case 'r': sb.Append('\r'); i += 2; break;
                                case 't': sb.Append('\t'); i += 2; break;
                                case 'b': sb.Append('\b'); i += 2; break;
                                case 'f': sb.Append('\f'); i += 2; break;
                                case 'u':
                                    if (i + 5 < json.Length)
                                    {
                                        var hex = json.Substring(i + 2, 4);
                                        if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                                System.Globalization.CultureInfo.InvariantCulture, out var code))
                                        {
                                            sb.Append((char)code);
                                            i += 6;
                                            break;
                                        }
                                    }
                                    // Malformed \u — keep literal
                                    sb.Append('\\');
                                    sb.Append('u');
                                    i += 2;
                                    break;
                                default:
                                    // Unknown escape — preserve verbatim (e.g. user passed raw "\n"
                                    // not yet escaped at the JSON layer)
                                    sb.Append('\\');
                                    sb.Append(esc);
                                    i += 2;
                                    break;
                            }
                            continue;
                        }
                        if (ch == '"') break;
                        sb.Append(ch);
                        i++;
                    }
                    value = sb.ToString();
                    i++; // Skip closing quote
                }
                else if (json[i] == '{' || json[i] == '[')
                {
                    // Nested object or array - keep as JSON
                    var startChar = json[i];
                    var endChar = startChar == '{' ? '}' : ']';
                    var braceCount = 1;
                    var valueStart = i;
                    i++;
                    var inStr = false;
                    var esc = false;
                    while (i < json.Length && braceCount > 0)
                    {
                        if (esc) { esc = false; i++; continue; }
                        if (json[i] == '\\') { esc = true; i++; continue; }
                        if (json[i] == '"') { inStr = !inStr; i++; continue; }
                        if (!inStr)
                        {
                            if (json[i] == startChar) braceCount++;
                            else if (json[i] == endChar) braceCount--;
                        }
                        i++;
                    }
                    value = json.Substring(valueStart, i - valueStart);
                }
                else
                {
                    // Primitive value (number, bool, null)
                    var valueStart = i;
                    while (i < json.Length && json[i] != ',' && json[i] != '}' && !char.IsWhiteSpace(json[i])) i++;
                    value = json.Substring(valueStart, i - valueStart);
                }

                dict[key] = value;

                // Skip to next comma or end
                while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                if (i < json.Length && json[i] == ',') i++;
            }

            return dict;
        }

        /// <summary>
        /// Convenience method to extract uri from params for resource requests.
        /// </summary>
        public static string ExtractUri(string paramsJson)
        {
            var dict = ParseToDictionary(paramsJson);
            return dict.GetValueOrDefault("uri", "");
        }
    }

    [Serializable]
    public class JsonRpcResponse
    {
        public string jsonrpc;
        public string id;
        public object result;
    }

    [Serializable]
    public class JsonRpcErrorResponse
    {
        public string jsonrpc;
        public string id;
        public JsonRpcError error;
    }

    [Serializable]
    public class JsonRpcError
    {
        public string type;
        public string message;
        public object details;
    }

    // Handler interfaces
    public interface IToolHandler
    {
        string[] SupportedMethods { get; }
        /// <summary>
        /// Handles a tool request. The paramsJson is the raw JSON string of the params object.
        /// Use JsonRpcParamsParser.ParseToDictionary(paramsJson) to parse it.
        /// </summary>
        object Handle(string method, string paramsJson);
    }

    public interface IResourceHandler
    {
        string[] SupportedUris { get; }
        /// <summary>
        /// Handles a resource request. The paramsJson is the raw JSON string of the params object.
        /// Use JsonRpcParamsParser.ParseToDictionary(paramsJson) to parse it.
        /// </summary>
        object Handle(string uri, string paramsJson);
    }
}
