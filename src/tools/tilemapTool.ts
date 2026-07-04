import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'tilemap';
const toolDescription = `Manage Unity Tilemaps for 2D level design.
Actions:
- create: Create a new Tilemap under a Grid (creates Grid if needed)
- get_info: Get tilemap details (bounds, tile count, orientation)
- set_tile: Place a tile at a cell position
- erase_tile: Remove a tile at a cell position
- fill_area: Fill a rectangular area with a tile
- clear: Remove all tiles from a tilemap
- get_tile: Inspect what tile is at a cell position
- get_bounds: Get the used cell bounds
- list: List all tilemaps in the scene
- list_tiles: List TileBase assets in a folder
- create_tile: Create a Tile asset from a sprite
- set_color: Set the color tint of a tile at a position
- compress_bounds: Compress tilemap bounds to remove empty space`;

const paramsSchema = z.object({
  action: z.enum([
    'create', 'get_info', 'set_tile', 'erase_tile', 'fill_area',
    'clear', 'get_tile', 'get_bounds', 'list', 'list_tiles',
    'create_tile', 'set_color', 'compress_bounds'
  ]).describe('Tilemap action to perform'),
  tilemapName: z.string().optional().describe('Name of the Tilemap GameObject'),
  tilemapId: z.string().optional().describe('Instance ID of the Tilemap'),
  name: z.string().optional().describe('Name for new tilemap (create)'),
  parentPath: z.string().optional().describe('Parent Grid path (create)'),
  sortingOrder: z.string().optional().describe('Sorting order (create)'),
  tilePath: z.string().optional().describe('Asset path to a TileBase or Tile'),
  x: z.string().optional().describe('Cell X position'),
  y: z.string().optional().describe('Cell Y position'),
  startX: z.string().optional().describe('Fill area start X'),
  startY: z.string().optional().describe('Fill area start Y'),
  endX: z.string().optional().describe('Fill area end X'),
  endY: z.string().optional().describe('Fill area end Y'),
  folder: z.string().optional().describe('Folder to search for tiles (default: Assets)'),
  spritePath: z.string().optional().describe('Sprite path for create_tile'),
  color: z.string().optional().describe('Color JSON for create_tile: {"r":1,"g":1,"b":1,"a":1}'),
  r: z.string().optional().describe('Red color component (0-1)'),
  g: z.string().optional().describe('Green color component (0-1)'),
  b: z.string().optional().describe('Blue color component (0-1)'),
  a: z.string().optional().describe('Alpha color component (0-1)')
});

export function registerTilemapTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
            response.message || `Tilemap action '${params.action}' failed`
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
