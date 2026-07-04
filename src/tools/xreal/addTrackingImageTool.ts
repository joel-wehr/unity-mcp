import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'add_tracking_image';
const toolDescription = `Adds an image to the image tracking database. The system will detect and track this image in the camera feed, returning its pose in 3D space.`;

const paramsSchema = z.object({
  imageName: z.string().describe('Unique name for this tracking image'),
  imagePath: z.string().describe('Path to the image file (PNG or JPG) in the Assets folder'),
  physicalWidth: z.number().min(0.01).max(10).describe('Physical width of the image in meters (required for accurate tracking)'),
  physicalHeight: z.number().min(0.01).max(10).optional().describe('Physical height of the image in meters (calculated from aspect ratio if not provided)'),
  trackingQuality: z.enum(['Low', 'Medium', 'High']).default('Medium').describe('Tracking quality vs performance trade-off'),
  enableAtRuntime: z.boolean().default(true).describe('Whether to track this image immediately'),
  maxTrackingDistance: z.number().min(0.1).max(20).default(5).describe('Maximum distance in meters for detection'),
  movingImage: z.boolean().default(false).describe('Whether the image may move (enables continuous tracking)'),
});

/**
 * Registers the Add Tracking Image tool with the MCP server.
 * This tool adds images for marker-based AR tracking.
 */
export function registerAddTrackingImageTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to add tracking image'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
