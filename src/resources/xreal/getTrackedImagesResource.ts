import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_tracked_images_resource';
const resourceUri = 'xreal://tracked_images';
const resourceMimeType = 'application/json';

/**
 * Registers the Tracked Images resource with the MCP server.
 * This resource provides all currently tracked images.
 */
export function registerGetTrackedImagesResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'List of all currently tracked images with their poses, tracking states, and detected sizes',
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
      response.message || 'Failed to fetch tracked images'
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
