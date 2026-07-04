import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_detected_planes_resource';
const resourceUri = 'xreal://detected_planes';
const resourceMimeType = 'application/json';

/**
 * Registers the Detected Planes resource with the MCP server.
 * This resource provides all detected environmental planes.
 */
export function registerGetDetectedPlanesResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'List of all detected planes in the environment including floor, walls, tables, and other surfaces with their poses and boundaries',
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
      response.message || 'Failed to fetch detected planes'
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
