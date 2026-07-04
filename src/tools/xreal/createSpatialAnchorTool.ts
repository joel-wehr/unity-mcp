import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'create_spatial_anchor';
const toolDescription = `Creates a spatial anchor at a specified position or on a detected plane. Spatial anchors persist across sessions and maintain their position in the real world.`;

const paramsSchema = z.object({
  anchorName: z.string().describe('Unique name for this spatial anchor'),
  position: z.object({
    x: z.number(),
    y: z.number(),
    z: z.number()
  }).optional().describe('World position for the anchor'),
  rotation: z.object({
    x: z.number(),
    y: z.number(),
    z: z.number(),
    w: z.number()
  }).optional().describe('Rotation quaternion for the anchor'),
  attachToPlane: z.string().optional().describe('ID of a detected plane to attach the anchor to'),
  planeOffset: z.object({
    x: z.number().default(0),
    y: z.number().default(0),
    z: z.number().default(0)
  }).optional().describe('Offset from the plane center when attaching to a plane'),
  persistent: z.boolean().default(true).describe('Save anchor for persistence across sessions'),
  cloudEnabled: z.boolean().default(false).describe('Enable cloud sharing of this anchor'),
  metadata: z.record(z.string()).optional().describe('Custom key-value metadata to store with the anchor'),
});

/**
 * Registers the Create Spatial Anchor tool with the MCP server.
 * This tool creates persistent spatial anchors.
 */
export function registerCreateSpatialAnchorTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  // Validate that either position or attachToPlane is provided
  if (!params.position && !params.attachToPlane) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Either position or attachToPlane must be provided'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create spatial anchor'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
