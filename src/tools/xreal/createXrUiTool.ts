import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'create_xr_ui';
const toolDescription = `Creates a world-space UI canvas configured for XR interaction. Sets up proper scaling, interaction raycasting, and visual settings for comfortable viewing in mixed reality.`;

const paramsSchema = z.object({
  canvasName: z.string().default('XR Canvas').describe('Name for the UI Canvas GameObject'),
  position: z.object({
    x: z.number().default(0),
    y: z.number().default(1.5),
    z: z.number().default(2)
  }).default({ x: 0, y: 1.5, z: 2 }).describe('World position for the canvas'),
  rotation: z.object({
    x: z.number().default(0),
    y: z.number().default(0),
    z: z.number().default(0)
  }).default({ x: 0, y: 0, z: 0 }).describe('Euler rotation for the canvas'),
  width: z.number().default(1).describe('Canvas width in meters'),
  height: z.number().default(0.6).describe('Canvas height in meters'),
  pixelsPerMeter: z.number().default(1000).describe('UI resolution (pixels per meter)'),
  interactionType: z.enum(['Ray', 'Poke', 'Both']).default('Both').describe('How users interact with this UI'),
  followHead: z.boolean().default(false).describe('UI follows head movement (tag-along behavior)'),
  followDistance: z.number().default(2).describe('Distance to maintain when following head'),
  lookAtCamera: z.boolean().default(true).describe('UI always faces the camera'),
  curvedCanvas: z.boolean().default(false).describe('Use curved canvas for better peripheral visibility'),
  curveRadius: z.number().default(3).describe('Curve radius in meters (if curved)'),
  addSampleContent: z.boolean().default(false).describe('Add sample UI elements (button, slider, text)'),
});

/**
 * Registers the Create XR UI tool with the MCP server.
 * This tool creates world-space UI for XR.
 */
export function registerCreateXrUiTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to create XR UI'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
