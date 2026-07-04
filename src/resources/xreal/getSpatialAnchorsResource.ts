import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_spatial_anchors';
const resourceUri = 'xreal://spatial_anchors';
const resourceMimeType = 'application/json';

/**
 * Registers the Spatial Anchors resource with the MCP server.
 * This resource provides all spatial anchors in the scene.
 */
export function registerGetSpatialAnchorsResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'List of all spatial anchors in the current scene with their positions, rotations, persistence status, and custom metadata',
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
      response.message || 'Failed to fetch spatial anchors'
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
