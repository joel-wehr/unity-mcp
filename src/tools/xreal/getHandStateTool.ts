import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'get_hand_state';
const toolDescription = `Gets the current state of tracked hands including joint positions, detected gestures, pinch strength, and tracking confidence. Returns detailed hand pose data.`;

const paramsSchema = z.object({
  hand: z.enum(['Left', 'Right', 'Both']).default('Both').describe('Which hand(s) to query'),
  includeJointPositions: z.boolean().default(true).describe('Include all joint world positions'),
  includeJointRotations: z.boolean().default(false).describe('Include joint rotation quaternions'),
  includeGestures: z.boolean().default(true).describe('Include detected gesture information'),
  includeVelocity: z.boolean().default(false).describe('Include hand velocity data'),
  coordinateSpace: z.enum(['World', 'Camera', 'Head']).default('World').describe('Coordinate space for position data'),
});

/**
 * Registers the Get Hand State tool with the MCP server.
 * This tool retrieves current hand tracking data.
 */
export function registerGetHandStateTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_TRACKING_LOST,
      response.message || 'Failed to get hand state - hand tracking may not be active'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response.handState || response, null, 2)
    }]
  };
}
