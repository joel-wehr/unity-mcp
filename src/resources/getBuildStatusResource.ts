import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { ResourceTemplate } from '@modelcontextprotocol/sdk/server/mcp.js';

const resourceUri = 'unity://build/{infoType}';
const resourceName = 'Unity Build Information';
const resourceDescription = `Build pipeline and configuration information.
Info types: settings, scenes, report, platforms
Example URIs:
- unity://build/settings - Current build settings
- unity://build/scenes - Scenes in build
- unity://build/report - Last build report
- unity://build/platforms - Available platforms`;

export function registerGetBuildStatusResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceUri}`);

  const resourceTemplate = new ResourceTemplate(resourceUri, {
    list: async () => {
      return {
        resources: [
          {
            uri: 'unity://build/settings',
            name: 'Build Settings',
            description: 'Current build target, output path, and options',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://build/scenes',
            name: 'Build Scenes',
            description: 'Scenes included in the build',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://build/report',
            name: 'Build Report',
            description: 'Last build report with sizes and warnings',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://build/platforms',
            name: 'Available Platforms',
            description: 'Installed build platforms and their status',
            mimeType: 'application/json'
          }
        ]
      };
    }
  });

  server.resource(
    resourceName,
    resourceTemplate,
    { description: resourceDescription },
    async (uri, variables) => {
      logger.info(`Fetching build info: ${uri.href}`);

      const infoType = variables.infoType as string;
      if (!infoType) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Info type is required in URI"
        );
      }

      const response = await mcpUnity.sendRequest({
        method: 'get_build_info',
        params: { infoType }
      });

      if (!response.success) {
        throw new McpUnityError(
          ErrorType.RESOURCE_FETCH,
          response.message || `Failed to fetch build info: ${infoType}`
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
