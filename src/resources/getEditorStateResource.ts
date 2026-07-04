import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';

const resourceUri = 'unity://editor_state';
const resourceName = 'Unity Editor State';
const resourceDescription = `Current Unity Editor state including:
- Play mode status (editing, playing, paused)
- Compilation state
- Editor focus
- Active scene
- Selection
- Inspector lock state
- Current tool
- Grid settings`;

export function registerGetEditorStateResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceUri}`);

  server.resource(
    resourceName,
    resourceUri,
    { description: resourceDescription },
    async (uri) => {
      logger.info(`Fetching editor state: ${uri.href}`);

      const response = await mcpUnity.sendRequest({
        method: 'get_editor_state',
        params: {}
      });

      if (!response.success) {
        throw new McpUnityError(
          ErrorType.RESOURCE_FETCH,
          response.message || 'Failed to fetch editor state'
        );
      }

      return {
        contents: [{
          uri: uri.href,
          mimeType: 'application/json',
          text: JSON.stringify(response, null, 2)
        }]
      };
    }
  );
}
