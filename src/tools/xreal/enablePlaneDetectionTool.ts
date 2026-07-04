import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'enable_plane_detection';
const toolDescription = `Enables or disables plane detection for spatial mapping. Detects horizontal surfaces (floors, tables) and vertical surfaces (walls) in the real environment.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable plane detection'),
  planeTypes: z.array(z.enum(['Horizontal', 'Vertical', 'Both'])).default(['Both']).describe('Types of planes to detect'),
  minPlaneArea: z.number().min(0.01).max(10).default(0.25).describe('Minimum plane area in square meters'),
  maxPlanes: z.number().int().min(1).max(100).default(20).describe('Maximum number of planes to track'),
  updateMode: z.enum(['Continuous', 'OnDemand']).default('Continuous').describe('How planes are updated'),
  visualizePlanes: z.boolean().default(false).describe('Show debug visualization of detected planes'),
  planeColor: z.string().default('#00FF00').describe('Visualization color in hex format'),
  mergePlanes: z.boolean().default(true).describe('Merge nearby coplanar surfaces'),
  classifyPlanes: z.boolean().default(true).describe('Classify planes as floor, ceiling, wall, table, etc.'),
});

/**
 * Registers the Enable Plane Detection tool with the MCP server.
 * This tool controls spatial plane detection.
 */
export function registerEnablePlaneDetectionTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
      response.message || 'Failed to configure plane detection'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
