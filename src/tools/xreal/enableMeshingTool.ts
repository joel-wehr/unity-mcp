import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'enable_meshing';
const toolDescription = `Enables or disables spatial meshing to generate 3D mesh geometry of the real environment. Used for occlusion, physics collisions, and spatial understanding.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable spatial meshing'),
  meshDensity: z.enum(['Low', 'Medium', 'High']).default('Medium').describe('Mesh triangle density'),
  updateRate: z.number().min(0.1).max(10).default(1).describe('Mesh update rate in seconds'),
  volumeSize: z.object({
    x: z.number().default(10),
    y: z.number().default(5),
    z: z.number().default(10)
  }).default({ x: 10, y: 5, z: 10 }).describe('Meshing volume size in meters (centered on camera)'),
  visualizeMesh: z.boolean().default(false).describe('Render the spatial mesh visibly'),
  meshMaterial: z.enum(['Wireframe', 'Solid', 'Transparent', 'Occlusion']).default('Occlusion').describe('Material type for mesh visualization'),
  generateColliders: z.boolean().default(true).describe('Generate mesh colliders for physics'),
  classifyMesh: z.boolean().default(false).describe('Classify mesh regions (floor, wall, ceiling, furniture)'),
  enableOcclusion: z.boolean().default(true).describe('Use mesh for real-world occlusion of virtual objects'),
});

/**
 * Registers the Enable Meshing tool with the MCP server.
 * This tool controls spatial mesh generation.
 */
export function registerEnableMeshingTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: z.infer<typeof paramsSchema>) => {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.XREAL_FEATURE_NOT_SUPPORTED,
      response.message || 'Failed to configure spatial meshing'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
