import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { ResourceTemplate } from '@modelcontextprotocol/sdk/server/mcp.js';

const resourceUri = 'unity://settings/{category}';
const resourceName = 'Unity Project Settings';
const resourceDescription = `Project settings by category.
Categories: player, quality, graphics, physics, time, audio, editor, input, tags_layers
Example URIs:
- unity://settings/player - PlayerSettings (name, version, icons, etc.)
- unity://settings/quality - QualitySettings
- unity://settings/graphics - GraphicsSettings
- unity://settings/physics - PhysicsSettings`;

export function registerGetProjectSettingsResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceUri}`);

  const resourceTemplate = new ResourceTemplate(resourceUri, {
    list: async () => {
      return {
        resources: [
          {
            uri: 'unity://settings/player',
            name: 'Player Settings',
            description: 'Company, product name, version, icons, splash, scripting',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/quality',
            name: 'Quality Settings',
            description: 'Quality levels, shadows, textures, anti-aliasing',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/graphics',
            name: 'Graphics Settings',
            description: 'Render pipeline, transparency sort, shaders',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/physics',
            name: 'Physics Settings',
            description: 'Gravity, layer collision matrix, solver iterations',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/time',
            name: 'Time Settings',
            description: 'Fixed timestep, max timestep, time scale',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/audio',
            name: 'Audio Settings',
            description: 'Global volume, DSP buffer, sample rate',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/editor',
            name: 'Editor Settings',
            description: 'Version control, asset serialization, sprite packer',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/input',
            name: 'Input Settings',
            description: 'Input axes and button mappings',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://settings/tags_layers',
            name: 'Tags and Layers',
            description: 'Custom tags and sorting layers',
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
      logger.info(`Fetching project settings: ${uri.href}`);

      const category = variables.category as string;
      if (!category) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Category is required in URI"
        );
      }

      const response = await mcpUnity.sendRequest({
        method: 'get_project_settings',
        params: { category }
      });

      if (!response.success) {
        throw new McpUnityError(
          ErrorType.RESOURCE_FETCH,
          response.message || `Failed to fetch settings: ${category}`
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
