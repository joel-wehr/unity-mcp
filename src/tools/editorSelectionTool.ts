import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'editor_selection';
const toolDescription = 'Gets or sets the current Unity Editor selection (GameObjects, assets, etc.)';
const paramsSchema = z.object({
  action: z.enum(['get', 'set', 'clear', 'add']).optional().describe('Action to perform (default: get)'),
  instanceIds: z.array(z.number()).optional().describe('Array of instance IDs to select (for set action)'),
  paths: z.array(z.string()).optional().describe('Array of GameObject paths or asset paths to select (for set action)'),
  instanceId: z.number().optional().describe('Single instance ID to add to selection (for add action)'),
  path: z.string().optional().describe('Single path to add to selection (for add action)')
});

/**
 * Creates and registers the Editor Selection tool with the MCP server
 * This tool allows getting and setting the current Unity Editor selection
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerEditorSelectionTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
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
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

/**
 * Handles editor selection operations in Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to execute editor selection action`
    );
  }

  // Format the results nicely for get action
  let resultText = response.message || `Editor selection action completed`;

  if (response.activeGameObject) {
    resultText += `\n\nActive GameObject: ${response.activeGameObject.name} (Path: ${response.activeGameObject.path})`;
  }

  if (response.gameObjects && response.gameObjects.length > 0) {
    resultText += '\n\nSelected GameObjects:\n';
    for (const go of response.gameObjects) {
      resultText += `- ${go.name} (ID: ${go.instanceId}, Path: ${go.path})\n`;
    }
  }

  if (response.assets && response.assets.length > 0) {
    resultText += '\nSelected Assets:\n';
    for (const asset of response.assets) {
      resultText += `- ${asset.name} (Type: ${asset.type}, Path: ${asset.assetPath})\n`;
    }
  }

  return {
    content: [{
      type: "text" as const,
      text: resultText
    }]
  };
}
