import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_xreal_device_state';
const resourceUri = 'xreal://device_state';
const resourceMimeType = 'application/json';

/**
 * Registers the XREAL Device State resource with the MCP server.
 * This resource provides real-time XREAL device status.
 */
export function registerGetDeviceStateResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'Real-time XREAL device state including connection status, tracking quality, battery level, and thermal state',
      mimeType: resourceMimeType
    },
    async (uri) => {
      try {
        return await resourceHandler(mcpUnity, uri, logger);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

async function resourceHandler(mcpUnity: McpUnity, uri: URL, logger: Logger): Promise<ReadResourceResult> {
  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {}
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to fetch XREAL device state'
    );
  }

  return {
    contents: [{
      uri: resourceUri,
      mimeType: resourceMimeType,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
