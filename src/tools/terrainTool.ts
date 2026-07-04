import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'terrain';
const toolDescription = `Manage Unity Terrains.
Actions:
- create: Create a new terrain with specified size and resolution
- get_info: Get terrain details (size, resolution, layers, trees, details)
- set_height: Set terrain height at a world position with brush radius
- get_height: Sample terrain height at a world position
- flatten: Flatten entire terrain to a uniform height
- set_detail: Paint detail/grass density at a position
- paint_texture: Paint a terrain layer/texture at a position
- add_tree: Add a tree instance at a normalized (0-1) position
- get_layers: List all terrain layers (textures)
- add_layer: Add a new terrain layer from a texture asset
- set_settings: Modify terrain rendering settings
- list: List all terrains in the scene`;

const paramsSchema = z.object({
  action: z.enum([
    'create', 'get_info', 'set_height', 'get_height', 'flatten',
    'set_detail', 'paint_texture', 'add_tree', 'get_layers',
    'add_layer', 'set_settings', 'list'
  ]).describe('Terrain action to perform'),
  terrainName: z.string().optional().describe('Name of the terrain GameObject'),
  terrainId: z.string().optional().describe('Instance ID of the terrain'),
  width: z.string().optional().describe('Terrain width (create)'),
  height: z.string().optional().describe('Terrain height / max elevation (create/set_height/flatten)'),
  length: z.string().optional().describe('Terrain length (create)'),
  heightmapResolution: z.string().optional().describe('Heightmap resolution (create, power of 2 + 1)'),
  assetPath: z.string().optional().describe('Path to save terrain data asset'),
  x: z.string().optional().describe('World X position or detail/alphamap X coordinate'),
  z: z.string().optional().describe('World Z position or detail/alphamap Z coordinate'),
  radius: z.string().optional().describe('Brush radius'),
  strength: z.string().optional().describe('Paint strength 0-1'),
  layer: z.string().optional().describe('Terrain layer index or detail layer index'),
  density: z.string().optional().describe('Detail density value'),
  prototypeIndex: z.string().optional().describe('Tree prototype index'),
  scale: z.string().optional().describe('Tree scale'),
  texturePath: z.string().optional().describe('Texture asset path for add_layer'),
  tileSize: z.string().optional().describe('Texture tile size for add_layer'),
  layerPath: z.string().optional().describe('Path to save the TerrainLayer asset'),
  drawHeightmap: z.string().optional().describe('Enable/disable heightmap rendering'),
  drawTreesAndFoliage: z.string().optional().describe('Enable/disable trees and foliage'),
  basemapDistance: z.string().optional().describe('Distance for basemap rendering'),
  detailObjectDistance: z.string().optional().describe('Max detail object distance'),
  treeDistance: z.string().optional().describe('Max tree rendering distance'),
  treeBillboardDistance: z.string().optional().describe('Distance to switch trees to billboards'),
  heightmapPixelError: z.string().optional().describe('Heightmap pixel error for LOD')
});

export function registerTerrainTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
            response.message || `Terrain action '${params.action}' failed`
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
