import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_passthrough';
const toolDescription = `Configures the camera passthrough for mixed reality mode. Passthrough shows the real world through the glasses with virtual content overlaid.`;

const paramsSchema = z.object({
  enabled: z.boolean().describe('Enable or disable passthrough'),
  blendMode: z.enum(['Opaque', 'Additive', 'AlphaBlend']).default('AlphaBlend').describe('How virtual content blends with passthrough'),
  brightness: z.number().min(0).max(2).default(1).describe('Passthrough brightness multiplier'),
  contrast: z.number().min(0).max(2).default(1).describe('Passthrough contrast adjustment'),
  saturation: z.number().min(0).max(2).default(1).describe('Passthrough color saturation'),
  edgeRendering: z.boolean().default(false).describe('Enable edge detection rendering for stylized look'),
  colorCorrection: z.boolean().default(true).describe('Enable automatic color correction'),
  environmentDepth: z.boolean().default(true).describe('Use environment depth for proper occlusion'),
});

/**
 * Registers the Configure Passthrough tool with the MCP server.
 * This tool controls MR passthrough settings.
 */
export function registerConfigurePassthroughTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to configure passthrough'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
