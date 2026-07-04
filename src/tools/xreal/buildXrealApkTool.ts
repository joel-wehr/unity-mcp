import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'build_xreal_apk';
const toolDescription = `Queues an asynchronous Android APK build for XREAL One Pro and returns immediately with a jobId.
Unity's BuildPipeline.BuildPlayer blocks the editor for minutes; this tool dispatches the build via
EditorApplication.delayCall so the MCP request doesn't time out. Poll get_build_status with the
returned jobId to follow progress and pick up the final BuildReport summary (output path, size,
build time, errors, warnings).`;

const paramsSchema = z.object({
  outputPath: z.string().optional().describe('Output path for the APK. Defaults to Builds/AppName.apk'),
  buildType: z.enum(['Debug', 'Release', 'Development']).default('Development').describe('Build configuration type'),
  scenes: z.array(z.string()).optional().describe('Scene paths to include (defaults to scenes in build settings)'),
  compressionMethod: z.enum(['Default', 'LZ4', 'LZ4HC']).default('LZ4').describe('APK compression method'),
  developmentBuild: z.boolean().default(true).describe('Include development features (profiler, debug logs)'),
  autoconnectProfiler: z.boolean().default(false).describe('Auto-connect Unity profiler on start'),
  deepProfilingSupport: z.boolean().default(false).describe('Enable deep profiling (slower builds)'),
  scriptDebugging: z.boolean().default(false).describe('Enable script debugging'),
  strictMode: z.boolean().default(false).describe('Enable strict mode for debugging'),
  buildAppBundle: z.boolean().default(false).describe('Build Android App Bundle (AAB) instead of APK'),
  splitApplicationBinary: z.boolean().default(false).describe('Split into base APK and expansion files'),
  runAfterBuild: z.boolean().default(false).describe('Automatically deploy and run after successful build'),
});

/**
 * Registers the Build XREAL APK tool with the MCP server.
 * This tool builds Android packages for XREAL devices.
 */
export function registerBuildXrealApkTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.BUILD_ERROR,
      response.message || 'Failed to build XREAL APK'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
