import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'find_gameobjects';
const toolDescription = 'Finds GameObjects in the scene by name pattern, tag, layer, or component type';
const paramsSchema = z.object({
  namePattern: z.string().optional().describe('Name pattern to search for (supports * wildcard, e.g. "Player*", "*Enemy*")'),
  tag: z.string().optional().describe('Tag to filter by (e.g. "Player", "Enemy")'),
  layer: z.number().optional().describe('Layer number to filter by'),
  componentType: z.string().optional().describe('Component type to filter by (e.g. "Camera", "Rigidbody", "BoxCollider")'),
  maxResults: z.number().optional().describe('Maximum number of results to return (default: 100)'),
  includeInactive: z.boolean().optional().describe('Whether to include inactive GameObjects (default: true)')
});

/**
 * Creates and registers the Find GameObjects tool with the MCP server
 * This tool allows searching for GameObjects in the Unity scene
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerFindGameObjectsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
 * Handles finding GameObjects in Unity
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
      response.message || `Failed to find GameObjects`
    );
  }

  // Format the results nicely
  let resultText = response.message || `Found GameObjects`;
  if (response.gameObjects && response.gameObjects.length > 0) {
    resultText += '\n\nResults:\n';
    for (const go of response.gameObjects) {
      resultText += `- ${go.name} (ID: ${go.instanceId}, Path: ${go.path})\n`;
      resultText += `  Tag: ${go.tag}, Layer: ${go.layerName}, Active: ${go.activeInHierarchy}\n`;
    }
  }

  return {
    content: [{
      type: response.type,
      text: resultText
    }]
  };
}
