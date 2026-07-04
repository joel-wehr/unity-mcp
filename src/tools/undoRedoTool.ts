import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'undo_redo';
const toolDescription = `Controls Unity's Undo/Redo system.
Actions:
- undo: Perform undo operation
- redo: Perform redo operation
- get_history: Get undo/redo history stack
- clear: Clear all undo history
- begin_group: Begin an undo group (combine multiple operations)
- end_group: End an undo group
- set_group_name: Set name for current undo group
- record_object: Record object state for undo
- flush: Flush all recorded operations`;

const paramsSchema = z.object({
  action: z.enum([
    'undo', 'redo', 'get_history', 'clear',
    'begin_group', 'end_group', 'set_group_name',
    'record_object', 'flush'
  ]).describe('Undo/Redo action to perform'),
  groupName: z.string().optional().describe('Name for undo group'),
  objectId: z.number().optional().describe('Instance ID of object to record'),
  objectPath: z.string().optional().describe('Path of object to record'),
  historyLimit: z.number().min(1).max(100).optional()
    .describe('Maximum number of history items to return (default: 20)')
});

export function registerUndoRedoTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const { action } = params;

  // Validate required parameters
  if ((action === 'begin_group' || action === 'set_group_name') && !params.groupName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'groupName' is required for ${action}`
    );
  }

  if (action === 'record_object' && !params.objectId && !params.objectPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'objectId' or 'objectPath' is required for record_object"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      groupName: params.groupName,
      objectId: params.objectId,
      objectPath: params.objectPath,
      historyLimit: params.historyLimit || 20
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Undo/Redo action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
