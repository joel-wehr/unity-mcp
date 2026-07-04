import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'setup_xr_interaction';
const toolDescription = `Sets up the XR Interaction Toolkit for XREAL development. Configures interaction systems, input actions, and default interactors for hand-based or controller-based interaction.`;

const paramsSchema = z.object({
  interactionMode: z.enum(['HandTracking', 'Controller', 'Both']).default('HandTracking').describe('Primary interaction mode'),
  installXRIT: z.boolean().default(true).describe('Install XR Interaction Toolkit package if not present'),
  xritVersion: z.string().optional().describe('Specific XR Interaction Toolkit version (defaults to latest compatible)'),
  createInputActions: z.boolean().default(true).describe('Create default XR input action mappings'),
  setupDefaultInteractors: z.boolean().default(true).describe('Add default ray and direct interactors'),
  enableTeleportation: z.boolean().default(false).describe('Enable teleportation locomotion system'),
  enableSnapTurn: z.boolean().default(false).describe('Enable snap turn rotation'),
  locomotionProvider: z.enum(['None', 'Continuous', 'Teleport', 'Both']).default('None').describe('Locomotion system to configure'),
  hapticFeedback: z.boolean().default(true).describe('Enable haptic feedback for interactions'),
});

/**
 * Registers the Setup XR Interaction tool with the MCP server.
 * This tool configures XR Interaction Toolkit.
 */
export function registerSetupXrInteractionTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to setup XR Interaction Toolkit'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
