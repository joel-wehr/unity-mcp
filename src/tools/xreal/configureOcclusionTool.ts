import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_occlusion';
const toolDescription = `Configures depth-based occlusion settings. Occlusion allows real-world objects to properly hide virtual objects that are behind them, creating realistic mixed reality.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable depth occlusion'),
  occlusionType: z.enum(['EnvironmentDepth', 'HumanSegmentation', 'Both']).default('EnvironmentDepth').describe('Type of occlusion to use'),
  depthMode: z.enum(['Best', 'Medium', 'Fastest']).default('Medium').describe('Depth estimation quality vs performance'),
  smoothEdges: z.boolean().default(true).describe('Apply edge smoothing to occlusion boundaries'),
  temporalFiltering: z.boolean().default(true).describe('Apply temporal filtering to reduce flickering'),
  humanBodyOcclusion: z.boolean().default(false).describe('Enable human body segmentation for person occlusion'),
  handOcclusion: z.boolean().default(true).describe('Enable occlusion by tracked hands'),
  occlusionLayers: z.array(z.string()).optional().describe('Specific layers that participate in occlusion'),
});

/**
 * Registers the Configure Occlusion tool with the MCP server.
 * This tool sets up depth-based occlusion.
 */
export function registerConfigureOcclusionTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
      ErrorType.XREAL_FEATURE_NOT_SUPPORTED,
      response.message || 'Failed to configure occlusion'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
