import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_hand_gestures';
const toolDescription = `Configures which hand gestures are recognized and their sensitivity. Gestures include pinch, grab, point, open palm, thumbs up, and more.`;

const paramsSchema = z.object({
  enabledGestures: z.array(z.enum([
    'Pinch',
    'Grab',
    'Point',
    'OpenPalm',
    'Fist',
    'ThumbsUp',
    'ThumbsDown',
    'Victory',
    'OK',
    'Rock',
    'Call'
  ])).default(['Pinch', 'Grab', 'Point', 'OpenPalm']).describe('List of gestures to recognize'),
  pinchThreshold: z.number().min(0).max(1).default(0.7).describe('Pinch detection threshold (0-1)'),
  grabThreshold: z.number().min(0).max(1).default(0.8).describe('Grab detection threshold (0-1)'),
  gestureHoldTime: z.number().min(0).max(2).default(0.1).describe('Time in seconds a gesture must be held before triggering'),
  smoothingFactor: z.number().min(0).max(1).default(0.5).describe('Gesture detection smoothing (0=none, 1=max)'),
  continuousMode: z.boolean().default(true).describe('Fire gesture events continuously while gesture is held'),
});

/**
 * Registers the Configure Hand Gestures tool with the MCP server.
 * This tool sets up gesture recognition parameters.
 */
export function registerConfigureHandGesturesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_CONFIGURATION_ERROR,
      response.message || 'Failed to configure hand gestures'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
