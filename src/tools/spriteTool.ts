import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'sprite';
const toolDescription = `Manage Unity Sprites and SpriteAtlases.
Actions:
- get_info: Get sprite import settings and sub-sprites
- list: List sprite assets in a folder
- set_import_settings: Configure texture type, pixels per unit, filter, compression
- slice: Slice a sprite sheet into multiple sprites (grid mode)
- get_sprite_renderers: List all SpriteRenderer components in scene
- set_sprite: Set the sprite on a SpriteRenderer
- get_atlases: List SpriteAtlas assets
- create_atlas: Create a new SpriteAtlas
- add_to_atlas: Add sprites/folders to a SpriteAtlas
- pack_atlas: Pack a SpriteAtlas for the active build target`;

const paramsSchema = z.object({
  action: z.enum([
    'get_info', 'list', 'set_import_settings', 'slice',
    'get_sprite_renderers', 'set_sprite', 'get_atlases',
    'create_atlas', 'add_to_atlas', 'pack_atlas'
  ]).describe('Sprite action to perform'),
  assetPath: z.string().optional().describe('Path to the sprite/texture asset'),
  folder: z.string().optional().describe('Folder to search (default: Assets)'),
  objectPath: z.string().optional().describe('Hierarchy path of a GameObject with SpriteRenderer'),
  objectId: z.string().optional().describe('Instance ID of a GameObject'),
  spritePath: z.string().optional().describe('Path to a sprite asset to assign'),
  // Import settings
  textureType: z.string().optional().describe('sprite or default'),
  spriteMode: z.string().optional().describe('single, multiple, or polygon'),
  pixelsPerUnit: z.string().optional().describe('Pixels per unit'),
  filterMode: z.string().optional().describe('point, bilinear, or trilinear'),
  maxTextureSize: z.string().optional().describe('Max texture size (e.g. 1024, 2048)'),
  compression: z.string().optional().describe('none, low, normal, or high'),
  // Slice settings
  sliceMode: z.string().optional().describe('Slice mode: grid'),
  cellWidth: z.string().optional().describe('Grid cell width in pixels'),
  cellHeight: z.string().optional().describe('Grid cell height in pixels'),
  offsetX: z.string().optional().describe('Grid offset X'),
  offsetY: z.string().optional().describe('Grid offset Y'),
  paddingX: z.string().optional().describe('Grid padding X'),
  paddingY: z.string().optional().describe('Grid padding Y'),
  // SpriteRenderer color
  r: z.string().optional().describe('Red (0-1)'),
  g: z.string().optional().describe('Green (0-1)'),
  b: z.string().optional().describe('Blue (0-1)'),
  a: z.string().optional().describe('Alpha (0-1)'),
  // Atlas settings
  atlasPath: z.string().optional().describe('Path to a SpriteAtlas asset'),
  spritePaths: z.string().optional().describe('Comma-separated sprite paths to add to atlas'),
  enableRotation: z.string().optional().describe('Allow rotation in atlas packing'),
  enableTightPacking: z.string().optional().describe('Enable tight packing'),
  padding: z.string().optional().describe('Atlas padding in pixels')
});

export function registerSpriteTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
        const response = await mcpUnity.sendRequest({
          method: toolName,
          params
        });

        if (!response.success) {
          throw new McpUnityError(
            ErrorType.TOOL_EXECUTION,
            response.message || `Sprite action '${params.action}' failed`
          );
        }

        return {
          content: [{
            type: "text" as const,
            text: JSON.stringify(response, null, 2)
          }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}
