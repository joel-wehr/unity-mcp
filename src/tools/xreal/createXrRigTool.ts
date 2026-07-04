import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'create_xr_rig';
const toolDescription = `Creates an XR Origin (camera rig) configured for XREAL One Pro. Sets up the head-mounted display camera, hand tracking origins, and interaction components.`;

const paramsSchema = z.object({
  rigName: z.string().default('XR Origin (XREAL)').describe('Name for the XR Origin GameObject'),
  position: z.object({
    x: z.number().default(0),
    y: z.number().default(0),
    z: z.number().default(0)
  }).default({ x: 0, y: 0, z: 0 }).describe('Initial position for the XR Origin'),
  trackingOriginMode: z.enum(['Device', 'Floor', 'Stage']).default('Device').describe('Tracking origin reference'),
  cameraYOffset: z.number().default(0).describe('Camera Y offset for floor-level tracking (typically 1.36m for standing)'),
  addHandControllers: z.boolean().default(true).describe('Add hand tracking controller objects'),
  addRayInteractors: z.boolean().default(true).describe('Add ray interactors for distant selection'),
  addDirectInteractors: z.boolean().default(true).describe('Add direct interactors for touch/grab'),
  addUIInteraction: z.boolean().default(true).describe('Add UI interaction components'),
  addLocomotionSystem: z.boolean().default(false).describe('Add locomotion system components'),
  createAsPrefab: z.boolean().default(false).describe('Save the rig as a prefab after creation'),
  prefabPath: z.string().optional().describe('Path to save prefab (if createAsPrefab is true)'),
});

/**
 * Registers the Create XR Rig tool with the MCP server.
 * This tool creates an XREAL-configured camera rig.
 */
export function registerCreateXrRigTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to create XR rig'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
