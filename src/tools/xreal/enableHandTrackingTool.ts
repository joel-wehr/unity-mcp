import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'enable_hand_tracking';
const toolDescription = `Enables or disables hand tracking on XREAL glasses. When enabled, the system tracks hand poses, joint positions, and recognizes gestures. Required for hand-based interactions.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable hand tracking'),
  trackingMode: z.enum(['Basic', 'Advanced']).default('Advanced').describe('Basic: faster, less accurate. Advanced: full joint tracking with higher accuracy'),
  gestureRecognition: z.boolean().default(true).describe('Enable gesture recognition (pinch, grab, point, etc.)'),
  handMeshVisualization: z.boolean().default(false).describe('Enable visual mesh rendering of tracked hands'),
  jointVisualization: z.boolean().default(false).describe('Enable debug visualization of hand joints'),
  trackedHands: z.enum(['Both', 'Left', 'Right']).default('Both').describe('Which hands to track'),
});

/**
 * Registers the Enable Hand Tracking tool with the MCP server.
 * This tool controls XREAL hand tracking.
 */
export function registerEnableHandTrackingTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to configure hand tracking'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
