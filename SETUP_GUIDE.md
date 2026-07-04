# XREAL Development Setup Guide

This guide will help you set up Unity for XREAL One Pro development with the MCP server integration.

## Prerequisites

- Unity 2021.3 LTS or newer
- Android SDK with API Level 29+ (Android 10+)
- Node.js 18+
- Samsung Galaxy S24 or compatible Android phone
- XREAL One Pro glasses

## Step 1: Install the Unity MCP Plugin

### Option A: Copy directly
Copy the `UnityPlugin` folder contents to your Unity project:

```
YourProject/
├── Packages/
│   └── com.joelwehr.unity-mcp/    <- Copy UnityPlugin contents here
├── Assets/
└── ProjectSettings/
```

### Option B: Add via Package Manager
Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.joelwehr.unity-mcp": "file:../path/to/unity-mcp/UnityPlugin"
  }
}
```

## Step 2: Start the MCP Server

In the unity-mcp directory:

```bash
npm install
npm run build
npm start
```

The server will start on port 8090 by default.

## Step 3: Configure Claude Code

Create `.mcp.json` in your Unity project root:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["C:/Users/joelw/Documents/GitHub/unity-mcp/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "LOGGING": "true"
      }
    }
  }
}
```

## Step 4: Start Unity MCP Bridge

In Unity Editor:
1. Go to **Tools > Unity MCP > Settings**
2. Click **Start** to start the WebSocket server
3. You should see "Server Running" status

## Step 5: Set Up XREAL Project

Use Claude Code to configure your project:

```
Use the setup_xreal_project tool to configure my Unity project for XREAL One Pro development
```

This will:
- Switch to Android build target
- Configure ARM64 architecture
- Set up IL2CPP scripting backend
- Configure graphics settings for mobile XR

## Step 6: Import NRSDK

Download NRSDK from [XREAL Developer Portal](https://developer.xreal.com/download)

Then use Claude Code:

```
Import NRSDK from local file: C:/Downloads/NRSDKForUnity_2.2.0.unitypackage
```

## Step 7: Validate Setup

Use Claude Code to verify everything is configured correctly:

```
Validate my XREAL project setup and fix any issues
```

## Quick Start Commands

### Create XR Rig
```
Create an XR Origin camera rig configured for XREAL with hand tracking support
```

### Enable Hand Tracking
```
Enable hand tracking with gesture recognition for pinch, grab, and point gestures
```

### Enable Plane Detection
```
Enable plane detection for horizontal and vertical surfaces
```

### Build APK
```
Build a development APK for XREAL and list connected devices
```

## Project Structure After Setup

```
YourProject/
├── Assets/
│   ├── NRSDK/                    # XREAL SDK
│   ├── Scenes/
│   │   └── MainScene.unity
│   ├── Prefabs/
│   │   └── XR Origin (XREAL).prefab
│   ├── Scripts/
│   ├── StreamingAssets/
│   │   └── TrackingImages/       # Image tracking configs
│   └── Editor/
│       └── McpSettings.asset     # MCP server settings
├── Builds/                       # APK output
├── ProjectSettings/
│   └── McpUnitySettings.json     # MCP connection config
└── .mcp.json                     # Claude Code config
```

## Troubleshooting

### MCP Server Won't Connect
1. Check Unity console for errors
2. Verify port 8090 is not in use
3. Ensure MCP server is running (`npm start`)
4. Check `ProjectSettings/McpUnitySettings.json` exists

### Build Fails
1. Verify Android SDK is installed
2. Check minimum SDK version (API 29+)
3. Ensure ARM64 architecture is selected
4. Verify IL2CPP backend is configured

### XREAL Glasses Not Detected
1. Connect phone via USB
2. Enable USB debugging on phone
3. Run `adb devices` to verify connection
4. Check phone supports DisplayPort Alt Mode

## Available MCP Tools

### Project Setup
- `setup_xreal_project` - Configure Unity project
- `configure_android_build` - Android settings
- `import_nrsdk` - Import NRSDK package
- `validate_xreal_setup` - Validate configuration

### Hand Tracking
- `enable_hand_tracking` - Enable hand tracking
- `get_hand_state` - Query hand poses
- `configure_hand_gestures` - Set up gestures
- `create_hand_interactable` - Make objects grabbable

### Spatial Mapping
- `enable_plane_detection` - Detect surfaces
- `get_detected_planes` - Query planes
- `create_spatial_anchor` - Create anchors
- `enable_meshing` - Generate spatial mesh

### Mixed Reality
- `configure_passthrough` - Camera passthrough
- `set_render_mode` - VR/AR/MR modes
- `configure_occlusion` - Depth occlusion

### Build & Deploy
- `build_xreal_apk` - Build APK
- `get_connected_devices` - List ADB devices

### XR Interaction
- `setup_xr_interaction` - Configure XR Interaction Toolkit
- `create_xr_rig` - Create camera rig
- `add_xr_interactor` - Add interactors
- `create_xr_ui` - Create XR UI canvas

### Performance
- `get_xr_performance_metrics` - Get metrics
- `profile_xr_scene` - Analyze scene
- `capture_xr_screenshot` - Take screenshot

## Resources

- [XREAL Developer Documentation](https://developer.xreal.com/docs)
- [Unity XR Development](https://docs.unity3d.com/Manual/XR.html)
- [NRSDK API Reference](https://developer.xreal.com/api)
