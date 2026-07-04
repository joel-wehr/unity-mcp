# Unity MCP Server

A unified Model Context Protocol (MCP) server that combines Unity Editor integration, Unity knowledge base search, and **comprehensive XREAL One Pro mixed reality development** capabilities for AI assistants.

## Features

### Core Unity Editor Integration (22 Tools)

| Tool | Description |
|------|-------------|
| `execute_menu_item` | Execute Unity menu items by path |
| `select_gameobject` | Select GameObjects in the editor |
| `get_gameobject` | Get detailed GameObject information |
| `update_gameobject` | Update or create GameObjects |
| `delete_gameobject` | Delete GameObjects from the scene |
| `duplicate_gameobject` | Duplicate GameObjects |
| `find_gameobjects` | Search for GameObjects by name/tag/layer/component |
| `update_component` | Add or modify components on GameObjects |
| `add_asset_to_scene` | Instantiate prefabs/assets in the scene |
| `create_prefab` | Create prefabs from GameObjects |
| `create_scene` | Create new scenes |
| `delete_scene` | Delete scenes |
| `load_scene` | Load scenes (single or additive) |
| `add_package` | Add packages via Package Manager |
| `manage_asset` | Move, copy, rename, delete assets |
| `run_tests` | Run Unity Test Runner tests |
| `send_console_log` | Send messages to Unity console |
| `get_console_logs` | Retrieve Unity console logs |
| `recompile_scripts` | Force script recompilation |
| `play_mode` | Control play mode (enter/exit/pause/step) |
| `editor_selection` | Get/set editor selection |
| `search_unity_knowledge` | Search Unity API docs via RAG |

---

## XREAL Mixed Reality Tools (32 Tools)

### Project Setup (4 Tools)

| Tool | Description |
|------|-------------|
| `setup_xreal_project` | Configure Unity project for XREAL One Pro (NRSDK, XR Plugin Management) |
| `configure_android_build` | Android build settings optimized for Samsung S24 + XREAL |
| `import_nrsdk` | Import NRSDK package from local file or URL |
| `validate_xreal_setup` | Validate project configuration and fix issues |

### Device Control (4 Tools)

| Tool | Description |
|------|-------------|
| `get_xreal_device_info` | Query connected device status, tracking state, battery |
| `set_tracking_mode` | Switch between 0DoF, 3DoF, and 6DoF tracking |
| `calibrate_glasses` | Trigger IPD, brightness, and tracking calibration |
| `get_camera_frame` | Capture RGB camera frame for CV/AR development |

### Hand Tracking (4 Tools)

| Tool | Description |
|------|-------------|
| `enable_hand_tracking` | Enable/configure hand tracking with gesture recognition |
| `get_hand_state` | Query hand pose, joint positions, detected gestures |
| `configure_hand_gestures` | Configure gesture detection (pinch, grab, point, etc.) |
| `create_hand_interactable` | Add hand interaction to GameObjects (grab, poke, hover) |

### Spatial Mapping (5 Tools)

| Tool | Description |
|------|-------------|
| `enable_plane_detection` | Detect horizontal/vertical planes (floors, walls, tables) |
| `get_detected_planes` | Query all detected planes with poses and classifications |
| `create_spatial_anchor` | Create persistent spatial anchors in the real world |
| `manage_spatial_anchors` | Load, save, delete, query spatial anchors |
| `enable_meshing` | Generate spatial mesh for physics and occlusion |

### Image Tracking (3 Tools)

| Tool | Description |
|------|-------------|
| `add_tracking_image` | Register images for marker-based AR tracking |
| `configure_image_tracking` | Set tracking quality, simultaneous image count |
| `get_tracked_images` | Query currently detected and tracked images |

### Mixed Reality (3 Tools)

| Tool | Description |
|------|-------------|
| `configure_passthrough` | Enable camera passthrough with blend settings |
| `set_render_mode` | Switch between VR, AR, and MR rendering modes |
| `configure_occlusion` | Set up depth-based real-world occlusion |

### Build & Deploy (2 Tools)

| Tool | Description |
|------|-------------|
| `build_xreal_apk` | Build optimized APK for XREAL devices |
| `get_connected_devices` | List ADB-connected Android devices |

### XR Interaction Toolkit (4 Tools)

| Tool | Description |
|------|-------------|
| `setup_xr_interaction` | Configure XR Interaction Toolkit for XREAL |
| `create_xr_rig` | Create XREAL-configured XR Origin camera rig |
| `add_xr_interactor` | Add ray/direct/poke interactors to controllers |
| `create_xr_ui` | Create world-space UI optimized for XR |

### Performance & Debug (3 Tools)

| Tool | Description |
|------|-------------|
| `get_xr_performance_metrics` | Real-time FPS, GPU, CPU, thermals, memory |
| `profile_xr_scene` | Analyze scene for XR performance issues |
| `capture_xr_screenshot` | Capture mono/stereo XR viewport screenshots |

---

## Resources

### Core Unity Resources (7)

| Resource | URI | Description |
|----------|-----|-------------|
| Scene Hierarchy | `unity://scenes_hierarchy` | All GameObjects in loaded scenes |
| GameObject | `unity://gameobject/{id}` | Detailed GameObject info |
| Menu Items | `unity://menu-items` | Available Unity menu items |
| Console Logs | `unity://logs/{type}` | Console logs with filtering |
| Packages | `unity://packages` | Package Manager packages |
| Assets | `unity://assets` | Asset Database contents |
| Tests | `unity://tests/{mode}` | Test Runner tests |

### XREAL Resources (6)

| Resource | URI | Description |
|----------|-----|-------------|
| Device State | `xreal://device_state` | Connection status, tracking quality, thermals |
| Hand Tracking | `xreal://hand_tracking/{hand}` | Real-time hand joint positions and gestures |
| Spatial Anchors | `xreal://spatial_anchors` | All spatial anchors in scene |
| Detected Planes | `xreal://detected_planes` | Environment surfaces with classifications |
| Tracked Images | `xreal://tracked_images` | Currently tracked image markers |
| Build Settings | `xreal://build_settings` | Android/XREAL build configuration |

---

## Prompts

### Core Unity Prompts (1)

| Prompt | Description |
|--------|-------------|
| `gameobject_handling_strategy` | Workflow guidance for GameObject manipulation |

### XREAL Prompts (4)

| Prompt | Description |
|--------|-------------|
| `xreal_project_setup` | Step-by-step guide for new XREAL projects |
| `hand_interaction_strategy` | Best practices for hand tracking UX |
| `spatial_anchor_workflow` | Creating persistent MR experiences |
| `xreal_optimization_guide` | Mobile XR performance optimization |

---

## Installation

```bash
npm install
npm run build
```

## Configuration

### Claude Code (Project-Level)

Create `.mcp.json` in your project:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["C:/path/to/unity-mcp/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "UNITY_HOST": "localhost",
        "LOGGING": "true"
      }
    }
  }
}
```

### Claude Desktop

Add to `%APPDATA%/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["C:/path/to/unity-mcp/build/index.js"],
      "env": {
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

### With RAG Knowledge Search

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["path/to/unity-mcp/build/index.js"],
      "env": {
        "UNITY_PORT": "8090",
        "RAG_PYTHON_PATH": "python",
        "RAG_SERVER_PATH": "path/to/unity-rag-server",
        "RAG_DB_PATH": "path/to/unity-rag-server/data"
      }
    }
  }
}
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `UNITY_PORT` | Unity WebSocket server port | `8090` |
| `UNITY_HOST` | Unity WebSocket server host | `localhost` |
| `LOGGING` | Enable console logging | `false` |
| `LOGGING_FILE` | Enable file logging | `false` |
| `RAG_PYTHON_PATH` | Path to Python executable | - |
| `RAG_SERVER_PATH` | Path to RAG server directory | - |
| `RAG_DB_PATH` | Path to RAG database | - |

---

## Quick Start: XREAL Development

### 1. Setup New XREAL Project

```
Use prompt: xreal_project_setup
Parameters: { projectType: "MR", targetDevice: "XREALOnePro" }
```

This will guide you through:
- Project configuration
- NRSDK import
- Android build settings
- XR rig creation
- Hand tracking setup

### 2. Create Hand-Interactive Object

```
Tool: create_hand_interactable
Parameters: {
  targetGameObject: "MyCube",
  interactionType: "Grab",
  highlightOnHover: true,
  hapticFeedback: true
}
```

### 3. Add Spatial Anchor

```
Tool: create_spatial_anchor
Parameters: {
  anchorName: "my_anchor",
  position: { x: 0, y: 1, z: 2 },
  persistent: true
}
```

### 4. Build and Deploy

```
Tool: build_xreal_apk
Parameters: {
  buildType: "Development",
  developmentBuild: true
}
```

---

## Architecture

```
+-------------------+     STDIO      +-------------------+
|   AI Assistant    | <-----------> |   Unity MCP       |
|   (Claude Code)   |               |   Server (Node)   |
+-------------------+               +--------+----------+
                                             |
                            WebSocket (8090) |
                                             v
                                    +--------+----------+
                                    |   Unity Editor    |
                                    |   + NRSDK         |
                                    +--------+----------+
                                             |
                                      USB/WiFi |
                                             v
                                    +--------+----------+
                                    |  Samsung S24      |
                                    |  + XREAL One Pro  |
                                    +-------------------+
```

---

## Development

```bash
# Build
npm run build

# Watch mode
npm run watch

# Run with MCP Inspector
npm run inspector

# Start
npm start
```

## File Structure

```
unity-mcp/
├── src/
│   ├── index.ts                    # Server entry point
│   ├── unity/
│   │   └── mcpUnity.ts             # Unity WebSocket bridge
│   ├── tools/
│   │   ├── *.ts                    # Core Unity tools (22)
│   │   └── xreal/                  # XREAL tools (32)
│   │       ├── setupXrealProjectTool.ts
│   │       ├── enableHandTrackingTool.ts
│   │       ├── createSpatialAnchorTool.ts
│   │       └── ...
│   ├── resources/
│   │   ├── *.ts                    # Core Unity resources (7)
│   │   └── xreal/                  # XREAL resources (6)
│   │       ├── getDeviceStateResource.ts
│   │       ├── getHandTrackingResource.ts
│   │       └── ...
│   ├── prompts/
│   │   ├── gameobjectHandlingPrompt.ts
│   │   └── xreal/                  # XREAL prompts (4)
│   │       ├── xrealProjectSetupPrompt.ts
│   │       ├── handInteractionPrompt.ts
│   │       └── ...
│   └── utils/
│       ├── logger.ts
│       └── errors.ts               # Includes XREAL error types
├── build/                          # Compiled JavaScript
├── .mcp.json                       # MCP server configuration
└── package.json
```

---

## Unity Side Requirements

The MCP server sends JSON-RPC messages to Unity. You need a Unity C# plugin that:

1. Listens on WebSocket port 8090
2. Handles incoming tool/resource requests
3. Interfaces with NRSDK for XREAL features
4. Returns JSON responses with `{ success: true/false, ... }`

---

## Summary

| Category | Count |
|----------|-------|
| **Core Unity Tools** | 22 |
| **XREAL Tools** | 32 |
| **Total Tools** | **54** |
| **Core Resources** | 7 |
| **XREAL Resources** | 6 |
| **Total Resources** | **13** |
| **Prompts** | 5 |

---

## License

MIT
