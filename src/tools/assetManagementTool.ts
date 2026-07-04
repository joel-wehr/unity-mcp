import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'manage_asset';
const toolDescription = 'Manages assets in the project: move, delete, rename, copy, create_folder, get_path';
const paramsSchema = z.object({
  action: z.enum(['move', 'delete', 'rename', 'copy', 'create_folder', 'get_path']).describe('The asset management action to perform'),
  sourcePath: z.string().optional().describe('Source asset path (for move, copy)'),
  destPath: z.string().optional().describe('Destination asset path (for move, copy)'),
  assetPath: z.string().optional().describe('Asset path (for delete, rename, get_path)'),
  newName: z.string().optional().describe('New name for the asset (for rename)'),
  parentFolder: z.string().optional().describe('Parent folder path (for create_folder)'),
  folderName: z.string().optional().describe('New folder name (for create_folder)'),
  guid: z.string().optional().describe('Asset GUID (for get_path)'),
  instanceId: z.number().optional().describe('Instance ID (for get_path)')
});

/**
 * Creates and registers the Asset Management tool with the MCP server
 * This tool allows managing assets in the Unity project
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerAssetManagementTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
 * Handles asset management operations in Unity
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
      response.message || `Failed to execute asset management action`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
