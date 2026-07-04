import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_connected_devices';
const toolDescription = `Lists all Android devices connected via ADB. Shows device IDs, models, Android versions, and connection status. Useful for verifying device setup before deployment.`;

const paramsSchema = z.object({
  includeEmulators: z.boolean().default(false).describe('Include Android emulators in the list'),
  includeUnauthorized: z.boolean().default(true).describe('Include devices pending USB debugging authorization'),
  checkXrealSupport: z.boolean().default(true).describe('Check if devices support XREAL glasses'),
  refreshDevices: z.boolean().default(true).describe('Refresh device list before returning'),
});

/**
 * Registers the Get Connected Devices tool with the MCP server.
 * This tool lists ADB-connected devices.
 */
export function registerGetConnectedDevicesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.ADB_ERROR,
      response.message || 'Failed to get connected devices. Is ADB installed and in PATH?'
    );
  }

  const devices = response.devices || [];
  if (devices.length === 0) {
    return {
      content: [{
        type: 'text' as const,
        text: 'No devices connected.\n\nTo connect a device:\n1. Enable USB debugging on your phone\n2. Connect via USB cable\n3. Accept the USB debugging prompt on the device\n4. Run this tool again'
      }]
    };
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(devices, null, 2)
    }]
  };
}
