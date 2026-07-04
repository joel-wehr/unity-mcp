import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'create_hand_interactable';
const toolDescription = `Adds hand interaction components to a GameObject, making it respond to hand gestures like pinch-to-grab, poke, or hover. Sets up the necessary colliders and interaction scripts.`;

const paramsSchema = z.object({
  targetGameObject: z.string().describe('Instance ID, name, or path of the GameObject to make interactable'),
  interactionType: z.enum(['Grab', 'Poke', 'Hover', 'All']).describe('Type of hand interaction to enable'),
  grabType: z.enum(['Kinematic', 'Physics', 'Instant']).default('Kinematic').describe('How the object moves when grabbed'),
  twoHandedGrab: z.boolean().default(false).describe('Allow grabbing with both hands simultaneously'),
  throwOnRelease: z.boolean().default(true).describe('Apply velocity when releasing grabbed object'),
  throwMultiplier: z.number().default(1.5).describe('Velocity multiplier for thrown objects'),
  hoverDistance: z.number().default(0.1).describe('Distance at which hover detection activates (meters)'),
  pokeDepth: z.number().default(0.02).describe('Depth required for poke interaction (meters)'),
  hapticFeedback: z.boolean().default(true).describe('Enable haptic feedback on interaction (if supported)'),
  highlightOnHover: z.boolean().default(true).describe('Visually highlight object when hand hovers over it'),
  highlightColor: z.string().default('#FFD700').describe('Highlight color in hex format'),
});

/**
 * Registers the Create Hand Interactable tool with the MCP server.
 * This tool adds hand interaction capabilities to GameObjects.
 */
export function registerCreateHandInteractableTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to create hand interactable'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
