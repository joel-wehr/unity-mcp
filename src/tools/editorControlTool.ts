import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'editor_control';
const toolDescription = `Controls Unity Editor windows and UI elements.
Actions:
- focus_window: Focus a specific editor window (Inspector, Hierarchy, Project, Scene, Game, Console, Profiler)
- get_windows: List all open editor windows
- open_window: Open an editor window
- close_window: Close an editor window
- inspector_lock: Lock/unlock the Inspector
- inspector_mode: Set Inspector to Normal or Debug mode
- ping_asset: Ping/highlight an asset in the Project window
- frame_selected: Frame selected object in Scene view
- set_scene_view: Configure Scene view settings (2D, orthographic, etc.)
- get_editor_state: Get current editor state (play mode, paused, compiling, etc.)
- refresh: Force refresh of Project window and AssetDatabase
- clear_console: Clear the Console window
- take_screenshot: Capture editor window screenshot`;

const paramsSchema = z.object({
  action: z.enum([
    'focus_window', 'get_windows', 'open_window', 'close_window',
    'inspector_lock', 'inspector_mode', 'ping_asset', 'frame_selected',
    'set_scene_view', 'get_editor_state', 'refresh', 'clear_console',
    'take_screenshot'
  ]).describe('Editor control action'),
  windowType: z.enum([
    'Inspector', 'Hierarchy', 'Project', 'Scene', 'Game', 'Console',
    'Profiler', 'Animation', 'Animator', 'Audio', 'Lighting',
    'Navigation', 'Occlusion', 'Package Manager', 'Preferences',
    'Project Settings', 'Build Settings'
  ]).optional().describe('Type of editor window'),
  locked: z.boolean().optional().describe('Lock state for Inspector'),
  debugMode: z.boolean().optional().describe('Debug mode for Inspector'),
  assetPath: z.string().optional().describe('Asset path for ping_asset'),
  sceneViewSettings: z.object({
    is2D: z.boolean().optional(),
    isOrthographic: z.boolean().optional(),
    showGrid: z.boolean().optional(),
    showGizmos: z.boolean().optional(),
    showSkybox: z.boolean().optional(),
    showFlares: z.boolean().optional(),
    showFog: z.boolean().optional(),
    showParticles: z.boolean().optional(),
    cameraPosition: z.object({
      x: z.number(), y: z.number(), z: z.number()
    }).optional(),
    cameraRotation: z.object({
      x: z.number(), y: z.number(), z: z.number()
    }).optional(),
    cameraSize: z.number().optional().describe('Orthographic size or perspective FOV')
  }).optional().describe('Scene view configuration'),
  screenshotPath: z.string().optional().describe('Path to save screenshot'),
  windowIndex: z.number().optional().describe('Window index when multiple of same type exist')
});

export function registerEditorControlTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const { action } = params;

  // Validate required parameters for specific actions
  if (['focus_window', 'open_window', 'close_window'].includes(action) && !params.windowType) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'windowType' is required for ${action}`
    );
  }

  if (action === 'ping_asset' && !params.assetPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'assetPath' is required for ping_asset"
    );
  }

  if (action === 'inspector_lock' && params.locked === undefined) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'locked' is required for inspector_lock"
    );
  }

  if (action === 'set_scene_view' && !params.sceneViewSettings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'sceneViewSettings' is required for set_scene_view"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Editor control action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
