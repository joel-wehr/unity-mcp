import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'navmesh';
const toolDescription = `Manage Unity NavMesh navigation system.
Actions:
- bake: Bake the NavMesh for the current scene
- clear: Clear all NavMesh data
- get_settings: Get NavMesh agent settings (radius, height, slope, climb)
- set_settings: Modify NavMesh agent settings
- get_areas: List NavMesh area names and costs
- set_area: Set area cost by index
- get_agents: List all NavMeshAgent components in scene
- add_agent: Add/configure NavMeshAgent on a GameObject
- find_path: Calculate a path between two positions
- sample_position: Find the nearest NavMesh point to a position
- get_obstacles: List all NavMeshObstacle components
- add_obstacle: Add NavMeshObstacle to a GameObject
- get_surfaces: List NavMeshSurface components (AI Navigation package)`;

const paramsSchema = z.object({
  action: z.enum([
    'bake', 'clear', 'get_settings', 'set_settings', 'get_areas',
    'set_area', 'get_agents', 'add_agent', 'find_path', 'sample_position',
    'get_obstacles', 'add_obstacle', 'get_surfaces'
  ]).describe('NavMesh action to perform'),
  objectPath: z.string().optional().describe('Hierarchy path of a GameObject'),
  objectId: z.string().optional().describe('Instance ID of a GameObject'),
  agentRadius: z.string().optional().describe('Agent radius for settings'),
  agentHeight: z.string().optional().describe('Agent height for settings'),
  agentSlope: z.string().optional().describe('Max slope angle for settings'),
  agentClimb: z.string().optional().describe('Step height for settings'),
  areaIndex: z.string().optional().describe('NavMesh area index (0-31)'),
  cost: z.string().optional().describe('Area traversal cost'),
  speed: z.string().optional().describe('Agent movement speed'),
  angularSpeed: z.string().optional().describe('Agent turning speed'),
  acceleration: z.string().optional().describe('Agent acceleration'),
  stoppingDistance: z.string().optional().describe('Distance to stop from target'),
  from: z.string().optional().describe('Start position JSON for find_path: {"x":0,"y":0,"z":0}'),
  to: z.string().optional().describe('End position JSON for find_path: {"x":0,"y":0,"z":0}'),
  x: z.string().optional().describe('X position for sample_position'),
  y: z.string().optional().describe('Y position for sample_position'),
  z: z.string().optional().describe('Z position for sample_position'),
  range: z.string().optional().describe('Search range for sample_position'),
  carving: z.string().optional().describe('Enable carving on obstacle (true/false)'),
  shape: z.string().optional().describe('Obstacle shape: box or capsule')
});

export function registerNavmeshTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
            response.message || `NavMesh action '${params.action}' failed`
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
