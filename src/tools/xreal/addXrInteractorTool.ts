import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'add_xr_interactor';
const toolDescription = `Adds XR interaction components to a GameObject. Interactors enable selection and manipulation of XR Interactables via ray casting or direct touch.`;

const paramsSchema = z.object({
  targetGameObject: z.string().describe('Instance ID, name, or path of the GameObject to add interactor to'),
  interactorType: z.enum(['Ray', 'Direct', 'Poke', 'Gaze']).describe('Type of interactor to add'),
  hand: z.enum(['Left', 'Right', 'Both', 'None']).default('None').describe('Which hand this interactor is associated with'),
  rayLength: z.number().default(10).describe('Ray interactor max length in meters'),
  rayWidth: z.number().default(0.01).describe('Ray visual width in meters'),
  rayValidColor: z.string().default('#00FF00').describe('Ray color when hovering valid target (hex)'),
  rayInvalidColor: z.string().default('#FF0000').describe('Ray color when hovering invalid target (hex)'),
  selectActionTrigger: z.enum(['State', 'StateChange', 'Toggle', 'Sticky']).default('StateChange').describe('How select action triggers'),
  enableHaptics: z.boolean().default(true).describe('Enable haptic feedback'),
  attachTransform: z.boolean().default(true).describe('Create attach point transform for grabbed objects'),
  lineType: z.enum(['Straight', 'BezierCurve', 'ProjectileCurve']).default('Straight').describe('Ray visual line type'),
});

/**
 * Registers the Add XR Interactor tool with the MCP server.
 * This tool adds interaction components to GameObjects.
 */
export function registerAddXrInteractorTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to add XR interactor'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
