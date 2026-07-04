import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'get_detected_planes';
const toolDescription = `Gets information about all currently detected planes in the environment. Returns plane positions, orientations, boundaries, and classifications.`;

const paramsSchema = z.object({
  planeType: z.enum(['All', 'Horizontal', 'Vertical']).default('All').describe('Filter by plane type'),
  classification: z.enum(['All', 'Floor', 'Ceiling', 'Wall', 'Table', 'Seat', 'Other']).default('All').describe('Filter by plane classification'),
  minArea: z.number().min(0).optional().describe('Minimum plane area filter (square meters)'),
  includeVertices: z.boolean().default(false).describe('Include boundary vertex positions'),
  includePose: z.boolean().default(true).describe('Include plane pose (position and rotation)'),
  coordinateSpace: z.enum(['World', 'Camera']).default('World').describe('Coordinate space for positions'),
});

/**
 * Registers the Get Detected Planes tool with the MCP server.
 * This tool retrieves current plane detection data.
 */
export function registerGetDetectedPlanesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to get detected planes'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response.planes || response, null, 2)
    }]
  };
}
