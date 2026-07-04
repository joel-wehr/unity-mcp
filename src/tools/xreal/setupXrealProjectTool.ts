import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'setup_xreal_project';
const toolDescription = `Sets up a Unity project for XREAL One Pro development. Configures Android build target, XR Plugin Management, and imports NRSDK. This is the first step for any new XREAL mixed reality project.`;

const paramsSchema = z.object({
  projectName: z.string().optional().describe('Optional project name to set in Player Settings'),
  companyName: z.string().optional().describe('Optional company name for the bundle identifier'),
  packageName: z.string().optional().describe('Custom package name (e.g., com.company.app). If not provided, will be generated from company and project names'),
  nrsdkVersion: z.string().optional().describe('NRSDK version to import (default: latest). Examples: "2.2.0", "2.1.0"'),
  enableHandTracking: z.boolean().default(true).describe('Enable hand tracking support in NRSDK configuration'),
  enableImageTracking: z.boolean().default(true).describe('Enable image tracking support'),
  enablePlaneDetection: z.boolean().default(true).describe('Enable plane detection for spatial mapping'),
  targetDevices: z.array(z.enum(['XREALLight', 'XREALAir', 'XREALAir2', 'XREALAir2Pro', 'XREALAir2Ultra', 'XREALOne', 'XREALOnePro'])).default(['XREALOnePro']).describe('Target XREAL devices to support'),
});

/**
 * Registers the Setup XREAL Project tool with the MCP server.
 * This tool configures a Unity project for XREAL mixed reality development.
 */
export function registerSetupXrealProjectTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
    params: {
      projectName: params.projectName,
      companyName: params.companyName,
      packageName: params.packageName,
      nrsdkVersion: params.nrsdkVersion,
      enableHandTracking: params.enableHandTracking,
      enableImageTracking: params.enableImageTracking,
      enablePlaneDetection: params.enablePlaneDetection,
      targetDevices: params.targetDevices,
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.XREAL_CONFIGURATION_ERROR,
      response.message || 'Failed to setup XREAL project'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
