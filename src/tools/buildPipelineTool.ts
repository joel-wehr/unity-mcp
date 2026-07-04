import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'build_pipeline';
const toolDescription = `Comprehensive Unity build pipeline control.
Actions:
- build: Execute a build with specified settings
- get_settings: Get current build settings
- set_settings: Modify build settings
- get_scenes: Get scenes in build settings
- set_scenes: Set scenes in build settings
- get_report: Get last build report (sizes, errors, warnings)
- get_player_settings: Get platform-specific player settings
- set_player_settings: Modify player settings
- switch_platform: Switch active build target
- get_platforms: List available build platforms
- validate: Validate build configuration without building`;

const paramsSchema = z.object({
  action: z.enum([
    'build', 'get_settings', 'set_settings', 'get_scenes', 'set_scenes',
    'get_report', 'get_player_settings', 'set_player_settings',
    'switch_platform', 'get_platforms', 'validate'
  ]).describe('Build pipeline action to perform'),
  platform: z.enum([
    'StandaloneWindows64', 'StandaloneOSX', 'StandaloneLinux64',
    'Android', 'iOS', 'WebGL', 'PS4', 'PS5', 'XboxOne', 'Switch', 'tvOS'
  ]).optional().describe('Target build platform'),
  buildPath: z.string().optional().describe('Output path for the build'),
  buildOptions: z.object({
    development: z.boolean().optional().describe('Development build with debugging'),
    allowDebugging: z.boolean().optional().describe('Allow script debugging'),
    autoRunPlayer: z.boolean().optional().describe('Run player after build'),
    connectProfiler: z.boolean().optional().describe('Connect profiler'),
    deepProfilingSupport: z.boolean().optional().describe('Deep profiling support'),
    enableHeadlessMode: z.boolean().optional().describe('Server/headless build'),
    strictMode: z.boolean().optional().describe('Strict mode'),
    compressWithLz4: z.boolean().optional().describe('LZ4 compression'),
    compressWithLz4HC: z.boolean().optional().describe('LZ4HC compression (slower, smaller)'),
    includeTestAssemblies: z.boolean().optional().describe('Include test assemblies'),
    buildScriptsOnly: z.boolean().optional().describe('Scripts only build'),
    cleanBuildCache: z.boolean().optional().describe('Clean build cache first')
  }).optional().describe('Build options'),
  scenes: z.array(z.object({
    path: z.string().describe('Scene asset path'),
    enabled: z.boolean().optional().describe('Whether scene is enabled in build')
  })).optional().describe('Scenes for build settings'),
  playerSettings: z.record(z.any()).optional().describe('Player settings to modify'),
  reportFilter: z.object({
    includeAssets: z.boolean().optional(),
    includeScenes: z.boolean().optional(),
    includeModules: z.boolean().optional(),
    minSizeKB: z.number().optional().describe('Minimum asset size to include')
  }).optional().describe('Filter for build report')
});

export function registerBuildPipelineTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  const { action } = params;

  // Validate required parameters
  if (action === 'build' && !params.buildPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'buildPath' is required for build action"
    );
  }

  if (action === 'switch_platform' && !params.platform) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'platform' is required for switch_platform action"
    );
  }

  if (action === 'set_scenes' && !params.scenes) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'scenes' is required for set_scenes action"
    );
  }

  if (action === 'set_player_settings' && !params.playerSettings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'playerSettings' is required for set_player_settings action"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.BUILD_ERROR,
      response.message || `Build pipeline action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
