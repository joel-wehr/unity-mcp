import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'get_tracked_images';
const toolDescription = `Gets the current state of tracked images including which images are detected, their poses, and tracking quality.`;

const paramsSchema = z.object({
  imageFilter: z.string().optional().describe('Filter by image name (supports wildcards)'),
  trackingState: z.enum(['All', 'Tracking', 'Limited', 'None']).default('All').describe('Filter by tracking state'),
  includePose: z.boolean().default(true).describe('Include position and rotation data'),
  includeSize: z.boolean().default(true).describe('Include detected size information'),
  coordinateSpace: z.enum(['World', 'Camera']).default('World').describe('Coordinate space for pose data'),
});

/**
 * Registers the Get Tracked Images tool with the MCP server.
 * This tool retrieves current image tracking data.
 */
export function registerGetTrackedImagesTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to get tracked images'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response.trackedImages || response, null, 2)
    }]
  };
}
