import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'duplicate_gameobject';
const toolDescription = 'Duplicates a GameObject in the scene, optionally with a new name';
const paramsSchema = z.object({
  objectPath: z.string().optional().describe('The path of the GameObject to duplicate (e.g. "Canvas/Panel/Button")'),
  objectName: z.string().optional().describe('The name of the GameObject to duplicate'),
  instanceId: z.number().optional().describe('The instance ID of the GameObject to duplicate'),
  newName: z.string().optional().describe('Optional new name for the duplicated GameObject')
});

/**
 * Creates and registers the Duplicate GameObject tool with the MCP server
 * This tool allows duplicating a GameObject in the Unity scene
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerDuplicateGameObjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
 * Handles duplicating a GameObject in Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  // Custom validation since we need at least one identifier
  if (params.objectPath === undefined && params.objectName === undefined && params.instanceId === undefined) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'objectPath', 'objectName' or 'instanceId' must be provided"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to duplicate GameObject`
    );
  }

  return {
    content: [{
      type: response.type,
      text: response.message || `Successfully duplicated GameObject`
    }]
  };
}
