import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'calibrate_glasses';
const toolDescription = `Triggers calibration procedures for XREAL glasses including IPD (interpupillary distance) adjustment, display brightness, and tracking recalibration.`;

const paramsSchema = z.object({
  calibrationType: z.enum(['ipd', 'brightness', 'tracking', 'all']).describe('Type of calibration to perform'),
  ipdValue: z.number().min(50).max(80).optional().describe('Manual IPD value in millimeters (typical range 50-80mm). If not provided, uses automatic calibration.'),
  brightnessLevel: z.number().min(0).max(100).optional().describe('Display brightness percentage (0-100)'),
  recenterPose: z.boolean().default(false).describe('Reset the current pose as the new origin'),
});

/**
 * Registers the Calibrate Glasses tool with the MCP server.
 * This tool performs XREAL glasses calibration.
 */
export function registerCalibrateGlassesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_DEVICE_NOT_CONNECTED,
      response.message || 'Failed to calibrate glasses'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
