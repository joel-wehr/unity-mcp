import { Logger } from '../../utils/logger.js';
import { ResourceTemplate, McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { ReadResourceResult } from '@modelcontextprotocol/sdk/types.js';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Variables } from '@modelcontextprotocol/sdk/shared/uriTemplate.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';

const resourceName = 'get_hand_tracking_state';
const resourceUri = 'xreal://hand_tracking/{hand}';
const resourceMimeType = 'application/json';

/**
 * Registers the Hand Tracking State resource with the MCP server.
 * This resource provides real-time hand tracking data.
 */
export function registerGetHandTrackingResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  const resourceTemplate = new ResourceTemplate(
    resourceUri,
    {
      list: undefined
    }
  );

  logger.info(`Registering resource: ${resourceName}`);

  server.resource(
    resourceName,
    resourceTemplate,
    {
      description: 'Real-time hand tracking data including joint positions, gestures, and tracking confidence. Use {hand} = "left", "right", or "both"',
      mimeType: resourceMimeType
    },
    async (uri, variables) => {
      try {
        return await resourceHandler(mcpUnity, uri, variables, logger);
      } catch (error) {
        logger.error(`Error handling resource ${resourceName}: ${error}`);
        throw error;
      }
    }
  );
}

async function resourceHandler(mcpUnity: McpUnity, uri: URL, variables: Variables, logger: Logger): Promise<ReadResourceResult> {
  const hand = decodeURIComponent(variables["hand"] as string);

  // Validate hand parameter
  const validHands = ['left', 'right', 'both'];
  if (!validHands.includes(hand.toLowerCase())) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Invalid hand parameter: "${hand}". Must be "left", "right", or "both"`
    );
  }

  const response = await mcpUnity.sendRequest({
    method: resourceName,
    params: {
      hand: hand.toLowerCase()
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.RESOURCE_FETCH,
      response.message || 'Failed to fetch hand tracking state'
    );
  }

  return {
    contents: [{
      uri: `xreal://hand_tracking/${hand}`,
      mimeType: resourceMimeType,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
