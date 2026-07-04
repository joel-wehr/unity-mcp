import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_xreal_build_settings';
const resourceUri = 'xreal://build_settings';
const resourceMimeType = 'application/json';

/**
 * Registers the XREAL Build Settings resource with the MCP server.
 * This resource provides current Android/XREAL build configuration.
 */
export function registerGetBuildSettingsResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceUri,
    {
      description: 'Current Android and XREAL build configuration including SDK versions, graphics settings, XR plugin settings, and player settings',
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
      response.message || 'Failed to fetch XREAL build settings'
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
