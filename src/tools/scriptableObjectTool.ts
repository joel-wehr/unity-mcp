import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'scriptable_object';
const toolDescription = `Manage Unity ScriptableObjects.
Actions:
- create: Create a new ScriptableObject asset
- get_properties: Inspect all serialized properties
- set_property: Set a serialized property value
- list: List ScriptableObjects in a folder
- find_by_type: Find all assets of a specific SO type
- duplicate: Duplicate a ScriptableObject asset`;

const paramsSchema = z.object({
  action: z.enum([
    'create', 'get_properties', 'set_property', 'list', 'find_by_type', 'duplicate'
  ]).describe('ScriptableObject action to perform'),
  typeName: z.string().optional().describe('Full or short type name of the ScriptableObject class'),
  assetPath: z.string().optional().describe('Path to the ScriptableObject asset'),
  destPath: z.string().optional().describe('Destination path for duplicate'),
  folder: z.string().optional().describe('Folder to search in (default: Assets)'),
  propertyName: z.string().optional().describe('Name of the serialized property'),
  propertyValue: z.string().optional().describe('Value to set')
});

export function registerScriptableObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
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
            response.message || `ScriptableObject action '${params.action}' failed`
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
