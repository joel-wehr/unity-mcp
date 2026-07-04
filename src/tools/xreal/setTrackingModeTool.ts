import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'set_tracking_mode';
const toolDescription = `Sets the tracking mode for the XREAL glasses. 0DoF provides rotation only, 3DoF adds positional tracking relative to start, and 6DoF provides full positional and rotational tracking.`;

const paramsSchema = z.object({
  mode: z.enum(['0DoF', '3DoF', '6DoF']).describe('Tracking mode to set'),
  recenterOnSwitch: z.boolean().default(true).describe('Recenter the tracking origin when switching modes'),
  trackingOrigin: z.enum(['Device', 'Floor', 'Stage']).default('Device').describe('Tracking origin reference point'),
});

/**
 * Registers the Set Tracking Mode tool with the MCP server.
 * This tool configures XREAL tracking behavior.
 */
export function registerSetTrackingModeTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to set tracking mode'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
