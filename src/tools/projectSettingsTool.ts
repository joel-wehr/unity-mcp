import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'project_settings';
const toolDescription = `Manages Unity Project Settings including PlayerSettings, QualitySettings, GraphicsSettings, PhysicsSettings, and more.
Actions: get (read settings), set (modify settings), list_categories (show available categories).
Categories: player, quality, graphics, physics, time, audio, editor, input, tags_layers, preset_manager`;

const paramsSchema = z.object({
  action: z.enum(['get', 'set', 'list_categories']).describe('The action to perform'),
  category: z.enum([
    'player', 'quality', 'graphics', 'physics', 'time', 'audio',
    'editor', 'input', 'tags_layers', 'preset_manager'
  ]).optional().describe('Settings category to read/modify'),
  platform: z.enum([
    'Standalone', 'Android', 'iOS', 'WebGL', 'PS4', 'PS5',
    'XboxOne', 'Switch', 'tvOS', 'LinuxHeadlessSimulation'
  ]).optional().describe('Target platform for platform-specific settings'),
  settings: z.record(z.any()).optional().describe('Key-value pairs of settings to modify'),
  qualityLevel: z.number().optional().describe('Quality level index (0-5 typically) for quality settings'),
  filter: z.string().optional().describe('Filter settings by name pattern (supports * wildcard)')
});

/**
 * Creates and registers the Project Settings tool with the MCP server
 * This tool provides comprehensive access to all Unity Project Settings
 */
export function registerProjectSettingsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  if (action === 'list_categories') {
    return {
      content: [{
        type: "text" as const,
        text: JSON.stringify({
          categories: [
            { name: 'player', description: 'PlayerSettings - company name, product name, version, icons, splash screen, scripting backend, etc.' },
            { name: 'quality', description: 'QualitySettings - quality levels, shadows, anti-aliasing, texture quality, etc.' },
            { name: 'graphics', description: 'GraphicsSettings - render pipeline, transparency sort, shader stripping, etc.' },
            { name: 'physics', description: 'PhysicsSettings - gravity, default material, layer collision matrix, etc.' },
            { name: 'time', description: 'TimeSettings - fixed timestep, maximum allowed timestep, time scale, etc.' },
            { name: 'audio', description: 'AudioSettings - global volume, DSP buffer size, sample rate, etc.' },
            { name: 'editor', description: 'EditorSettings - version control, asset serialization, sprite packer, etc.' },
            { name: 'input', description: 'InputSettings - input axes, button mappings (legacy input manager)' },
            { name: 'tags_layers', description: 'TagManager - tags and sorting layers' },
            { name: 'preset_manager', description: 'PresetManager - default presets for importers and components' }
          ]
        }, null, 2)
      }]
    };
  }

  if (!params.category && action !== 'list_categories') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'category' is required for get/set actions"
    );
  }

  if (action === 'set' && !params.settings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'settings' is required for set action"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      category: params.category,
      platform: params.platform,
      settings: params.settings,
      qualityLevel: params.qualityLevel,
      filter: params.filter
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to ${action} project settings`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
