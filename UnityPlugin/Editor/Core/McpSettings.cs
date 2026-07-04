using UnityEngine;
using UnityEditor;
using System.IO;

namespace UnityMcp.Editor.Core
{
    /// <summary>
    /// Settings for the MCP Unity Bridge.
    /// Stores configuration like port, timeout, and auto-start preferences.
    /// </summary>
    public class McpSettings : ScriptableObject
    {
        private const string SettingsPath = "Assets/Editor/McpSettings.asset";

        [SerializeField] private int _port = 8090;
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private int _requestTimeout = 10;
        [SerializeField] private bool _enableLogging = true;
        [SerializeField] private bool _enableXrealTools = true;

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                Save();
            }
        }

        public bool AutoStart
        {
            get => _autoStart;
            set
            {
                _autoStart = value;
                Save();
            }
        }

        public int RequestTimeout
        {
            get => _requestTimeout;
            set
            {
                _requestTimeout = value;
                Save();
            }
        }

        public bool EnableLogging
        {
            get => _enableLogging;
            set
            {
                _enableLogging = value;
                Save();
            }
        }

        public bool EnableXrealTools
        {
            get => _enableXrealTools;
            set
            {
                _enableXrealTools = value;
                Save();
            }
        }

        private static McpSettings _instance;
        private static bool _triedLoading;

        public static McpSettings Instance
        {
            get
            {
                if (_instance == null && !_triedLoading)
                {
                    _triedLoading = true;
                    _instance = AssetDatabase.LoadAssetAtPath<McpSettings>(SettingsPath);

                    // If still null, create in-memory only (don't save to disk automatically)
                    if (_instance == null)
                    {
                        _instance = CreateInstance<McpSettings>();
                    }
                }
                return _instance;
            }
        }

        public static void CreateSettingsAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<McpSettings>(SettingsPath) != null)
                return;

            var settings = CreateInstance<McpSettings>();
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            _instance = settings;
        }

        private void Save()
        {
            if (AssetDatabase.Contains(this))
            {
                EditorUtility.SetDirty(this);
            }
        }
    }

    /// <summary>
    /// Editor window for managing MCP settings and server status.
    /// </summary>
    public class McpSettingsWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Unity MCP/Settings", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<McpSettingsWindow>("MCP Settings");
            window.minSize = new Vector2(400, 300);
        }

        [MenuItem("Tools/Unity MCP/Start Server", priority = 0)]
        public static void StartServer()
        {
            McpUnityBridge.Start();
        }

        [MenuItem("Tools/Unity MCP/Stop Server", priority = 1)]
        public static void StopServer()
        {
            McpUnityBridge.Stop();
        }

        [MenuItem("Tools/Unity MCP/Restart Server", priority = 2)]
        public static void RestartServer()
        {
            McpUnityBridge.Restart();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);

            // Status section
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var statusColor = McpUnityBridge.IsRunning ? Color.green : Color.red;
                var statusText = McpUnityBridge.IsRunning ? "Running" : "Stopped";

                var oldColor = GUI.color;
                GUI.color = statusColor;
                EditorGUILayout.LabelField($"Status: {statusText}", EditorStyles.boldLabel, GUILayout.Width(150));
                GUI.color = oldColor;

                if (McpUnityBridge.IsRunning)
                {
                    EditorGUILayout.LabelField($"Port: {McpUnityBridge.Port}", GUILayout.Width(80));
                    EditorGUILayout.LabelField($"Clients: {McpUnityBridge.ClientCount}");
                }
            }

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(McpUnityBridge.IsRunning ? "Stop" : "Start", GUILayout.Width(80)))
                {
                    if (McpUnityBridge.IsRunning)
                        McpUnityBridge.Stop();
                    else
                        McpUnityBridge.Start();
                }

                if (GUILayout.Button("Restart", GUILayout.Width(80)))
                {
                    McpUnityBridge.Restart();
                }
            }

            EditorGUILayout.Space(20);

            // Settings section
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            var settings = McpSettings.Instance;

            EditorGUI.BeginChangeCheck();

            var newPort = EditorGUILayout.IntField("Port", settings.Port);
            if (newPort != settings.Port && newPort > 0 && newPort < 65536)
            {
                settings.Port = newPort;
            }

            settings.AutoStart = EditorGUILayout.Toggle("Auto Start", settings.AutoStart);
            settings.RequestTimeout = EditorGUILayout.IntSlider("Request Timeout (s)", settings.RequestTimeout, 5, 60);
            settings.EnableLogging = EditorGUILayout.Toggle("Enable Logging", settings.EnableLogging);
            settings.EnableXrealTools = EditorGUILayout.Toggle("Enable XREAL Tools", settings.EnableXrealTools);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.Space(20);

            // Help section
            EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The MCP Unity Bridge allows AI assistants to control Unity Editor.\n\n" +
                "Configure your MCP client to connect to:\n" +
                $"  ws://localhost:{settings.Port}/McpUnity\n\n" +
                "Supported tools: GameObject, Scene, Component, Asset, Editor, Tests, Console, XREAL",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            // XREAL section
            EditorGUILayout.LabelField("XREAL Development", EditorStyles.boldLabel);

            var nrsdkInstalled = IsNrsdkInstalled();
            var xrManagementInstalled = IsXrManagementInstalled();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("NRSDK:", GUILayout.Width(100));
                var color = nrsdkInstalled ? Color.green : Color.yellow;
                var oldColor2 = GUI.color;
                GUI.color = color;
                EditorGUILayout.LabelField(nrsdkInstalled ? "Installed" : "Not Found");
                GUI.color = oldColor2;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("XR Management:", GUILayout.Width(100));
                var color = xrManagementInstalled ? Color.green : Color.yellow;
                var oldColor2 = GUI.color;
                GUI.color = color;
                EditorGUILayout.LabelField(xrManagementInstalled ? "Installed" : "Not Found");
                GUI.color = oldColor2;
            }

            if (!nrsdkInstalled)
            {
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Setup XREAL Project"))
                {
                    // This would trigger the setup wizard
                    Debug.Log("[MCP] Use the 'setup_xreal_project' MCP tool to configure XREAL development");
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool IsNrsdkInstalled()
        {
            // Check for NRSDK presence
            return System.IO.Directory.Exists("Assets/NRSDK") ||
                   System.Type.GetType("NRKernal.NRInput, Assembly-CSharp") != null ||
                   System.Type.GetType("NRKernal.NRInput, NRSDK") != null;
        }

        private bool IsXrManagementInstalled()
        {
            #if XR_MANAGEMENT_ENABLED
            return true;
            #else
            return false;
            #endif
        }
    }
}
