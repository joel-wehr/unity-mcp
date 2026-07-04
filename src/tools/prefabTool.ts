import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'prefab';
const toolDescription = `Advanced Unity prefab operations.
Actions:
- get_info: Get prefab status (instance/asset type, variant, overrides)
- create_variant: Create a prefab variant from a base prefab
- get_overrides: List property modifications, added/removed components
- apply_overrides: Apply all instance overrides back to the prefab asset
- revert_overrides: Revert all instance overrides
- unpack: Unpack a prefab instance (outermost or completely)
- open: Open prefab in prefab editing mode
- close: Close prefab editing mode, return to main stage
- instantiate: Instantiate a prefab into the scene with optional position/parent`;

const paramsSchema = z.object({
  action: z.enum([
    'get_info', 'create_variant', 'get_overrides', 'apply_overrides',
    'revert_overrides', 'unpack', 'open', 'close', 'instantiate'
  ]).describe('Prefab action to perform'),
  assetPath: z.string().optional().describe('Path to the prefab asset (e.g. Assets/Prefabs/Player.prefab)'),
  variantPath: z.string().optional().describe('Destination path for create_variant'),
  objectPath: z.string().optional().describe('Hierarchy path of a prefab instance in the scene'),
  objectId: z.string().optional().describe('Instance ID of a prefab instance'),
  position: z.string().optional().describe('JSON position object for instantiate, e.g. {"x":0,"y":1,"z":0}'),
  parentPath: z.string().optional().describe('Hierarchy path of parent for instantiate'),
  completely: z.string().optional().describe('For unpack: "true" to unpack completely, "false" for outermost only')
});

export function registerPrefabTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: toolName,
          params
        });

        if (!response.success) {
          throw new McpUnityError(
            ErrorType.TOOL_EXECUTION,
            response.message || `Prefab action '${params.action}' failed`
          );
        }

        return {
          content: [{
            type: "text" as const,
            text: JSON.stringify(response, null, 2)
          }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}
