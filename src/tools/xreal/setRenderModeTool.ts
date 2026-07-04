import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'set_render_mode';
const toolDescription = `Sets the rendering mode for the XREAL experience. VR mode renders only virtual content, AR mode overlays on passthrough, MR mode enables full mixed reality with occlusion.`;

const paramsSchema = z.object({
  mode: z.enum(['VR', 'AR', 'MR']).describe('Rendering mode: VR (virtual only), AR (overlay), MR (full mixed reality)'),
  backgroundType: z.enum(['Solid', 'Skybox', 'Passthrough', 'None']).default('Passthrough').describe('Background rendering type'),
  backgroundColor: z.string().default('#000000').describe('Background color when using solid color (hex)'),
  enableOcclusion: z.boolean().default(true).describe('Enable real-world occlusion of virtual objects (MR mode)'),
  occlusionMode: z.enum(['HardOcclusion', 'SoftOcclusion', 'EnvironmentDepth']).default('EnvironmentDepth').describe('Occlusion quality mode'),
  stereoRenderingMode: z.enum(['MultiPass', 'SinglePassInstanced']).default('SinglePassInstanced').describe('Stereo rendering mode for performance'),
});

/**
 * Registers the Set Render Mode tool with the MCP server.
 * This tool configures the XR rendering mode.
 */
export function registerSetRenderModeTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_CONFIGURATION_ERROR,
      response.message || 'Failed to set render mode'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
