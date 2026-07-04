import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'get_xreal_device_info';
const toolDescription = `Gets information about the connected XREAL device including model, connection status, tracking state, battery level, and supported features. Works in both Editor simulation and on-device.`;

const paramsSchema = z.object({
  includeCapabilities: z.boolean().default(true).describe('Include device capability information (hand tracking, eye tracking, etc.)'),
  includeTrackingState: z.boolean().default(true).describe('Include current tracking state and quality'),
  includeBatteryInfo: z.boolean().default(true).describe('Include battery status if available'),
  includeDisplayInfo: z.boolean().default(true).describe('Include display resolution and refresh rate'),
});

/**
 * Registers the Get XREAL Device Info tool with the MCP server.
 * This tool retrieves information about connected XREAL hardware.
 */
export function registerGetXrealDeviceInfoTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_DEVICE_NOT_CONNECTED,
      response.message || 'Failed to get XREAL device information'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response.deviceInfo || response, null, 2)
    }]
  };
}
