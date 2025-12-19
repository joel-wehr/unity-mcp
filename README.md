# Unity MCP Server

A unified Model Context Protocol (MCP) server that combines Unity Editor integration and Unity knowledge base search capabilities for AI assistants.

## Features

### Unity Editor Integration (21 Tools)

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

### Unity Knowledge Search (1 Tool)

| Tool | Description |
|------|-------------|
| `search_unity_knowledge` | Search Unity API docs, manual, and local assets via RAG |

### Resources (7)

| Resource | URI | Description |
|----------|-----|-------------|
| Scene Hierarchy | `unity://scenes_hierarchy` | All GameObjects in loaded scenes |
| GameObject | `unity://gameobject/{id}` | Detailed GameObject info |
| Menu Items | `unity://menu-items` | Available Unity menu items |
| Console Logs | `unity://logs/{type}` | Console logs with filtering |
| Packages | `unity://packages` | Package Manager packages |
| Assets | `unity://assets` | Asset Database contents |
| Tests | `unity://tests/{mode}` | Test Runner tests |

## Installation

```bash
npm install
npm run build
```

## Usage

### Basic Configuration

Add to your MCP configuration (e.g., `.mcp.json`):

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "node",
      "args": ["path/to/unity-mcp/build/index.js"],
      "env": {
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

### With RAG Knowledge Search

To enable Unity documentation search, configure the RAG server:

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

## Unity Editor Setup

1. Install the MCP Unity package in your Unity project
2. Open `Tools > MCP Unity > Server Window`
3. Click "Start Server" (default port: 8090)
4. The MCP server will connect automatically

## Architecture

```
+-------------------+     STDIO      +-------------------+
|   AI Assistant    | <-----------> |   Unity MCP       |
|   (Claude, etc)   |               |   Server (Node)   |
+-------------------+               +--------+----------+
                                             |
                            WebSocket (8090) |
                                             v
                                    +--------+----------+
                                    |   Unity Editor    |
                                    |   (C# Plugin)     |
                                    +-------------------+
```

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

## License

MIT
