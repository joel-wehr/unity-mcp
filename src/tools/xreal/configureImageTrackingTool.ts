import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_image_tracking';
const toolDescription = `Configures image tracking settings including how many images can be tracked simultaneously, tracking quality, and update frequency.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable image tracking'),
  maxSimultaneousImages: z.number().int().min(1).max(20).default(4).describe('Maximum images to track at once'),
  trackingMode: z.enum(['Static', 'Dynamic']).default('Dynamic').describe('Static: images don\'t move. Dynamic: continuous tracking of moving images'),
  requestedTrackingMode: z.enum(['Default', 'Fast', 'Accurate']).default('Default').describe('Performance vs accuracy trade-off'),
  autoFocus: z.boolean().default(true).describe('Enable camera auto-focus for better detection'),
  lightEstimation: z.boolean().default(true).describe('Enable light estimation from tracked images'),
});

/**
 * Registers the Configure Image Tracking tool with the MCP server.
 * This tool configures image tracking behavior.
 */
export function registerConfigureImageTrackingTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to configure image tracking'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
