import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'manage_spatial_anchors';
const toolDescription = `Manages spatial anchors: load from persistence, save current anchors, delete anchors, or query anchor information. Essential for persistent MR experiences.`;

const paramsSchema = z.object({
  action: z.enum(['load', 'save', 'delete', 'query', 'list', 'clear_all']).describe('Action to perform on spatial anchors'),
  anchorId: z.string().optional().describe('Specific anchor ID for load/delete/query actions'),
  anchorName: z.string().optional().describe('Anchor name for load/delete/query actions (alternative to anchorId)'),
  filter: z.object({
    persistent: z.boolean().optional(),
    cloudEnabled: z.boolean().optional(),
    namePattern: z.string().optional().describe('Regex pattern to match anchor names'),
  }).optional().describe('Filter criteria for list action'),
  includeMetadata: z.boolean().default(true).describe('Include custom metadata in query results'),
  includeTransform: z.boolean().default(true).describe('Include position/rotation in query results'),
});

/**
 * Registers the Manage Spatial Anchors tool with the MCP server.
 * This tool handles spatial anchor lifecycle operations.
 */
export function registerManageSpatialAnchorsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: z.infer<typeof paramsSchema>) => {
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

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  // Validate action-specific requirements
  if (['load', 'delete', 'query'].includes(params.action) && !params.anchorId && !params.anchorName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `${params.action} action requires anchorId or anchorName`
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to ${params.action} spatial anchor(s)`
    );
  }

  // Format response based on action
  if (params.action === 'list' || params.action === 'query') {
    return {
      content: [{
        type: 'text' as const,
        text: JSON.stringify(response.anchors || response, null, 2)
      }]
    };
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
