import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_android_build';
const toolDescription = `Configures Android build settings optimized for XREAL One Pro on Samsung S24. Sets up ARM64 architecture, IL2CPP scripting backend, minimum API level, and other required settings for mobile XR.`;

const paramsSchema = z.object({
  minSdkVersion: z.number().int().min(26).max(34).default(29).describe('Minimum Android SDK version (default 29 for XREAL compatibility)'),
  targetSdkVersion: z.number().int().min(29).max(34).default(33).describe('Target Android SDK version'),
  scriptingBackend: z.enum(['IL2CPP', 'Mono']).default('IL2CPP').describe('Scripting backend (IL2CPP recommended for release)'),
  architectures: z.array(z.enum(['ARM64', 'ARMv7'])).default(['ARM64']).describe('Target CPU architectures'),
  installLocation: z.enum(['Auto', 'Internal', 'External']).default('Auto').describe('APK install location preference'),
  internetAccess: z.enum(['Auto', 'Require']).default('Auto').describe('Internet access requirement'),
  writePermission: z.enum(['Internal', 'External']).default('External').describe('Write access permission for storage'),
  graphicsApis: z.array(z.enum(['OpenGLES3', 'Vulkan'])).default(['OpenGLES3']).describe('Graphics APIs to use (OpenGLES3 recommended for XREAL)'),
  multithreadedRendering: z.boolean().default(true).describe('Enable multithreaded rendering'),
  staticBatching: z.boolean().default(true).describe('Enable static batching'),
  dynamicBatching: z.boolean().default(false).describe('Enable dynamic batching'),
  gpuSkinning: z.boolean().default(true).describe('Enable GPU skinning'),
  optimizeMeshData: z.boolean().default(true).describe('Strip unused mesh data'),
});

/**
 * Registers the Configure Android Build tool with the MCP server.
 * This tool sets up Android build settings for XREAL development.
 */
export function registerConfigureAndroidBuildTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_CONFIGURATION_ERROR,
      response.message || 'Failed to configure Android build settings'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
