import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'import_nrsdk';
const toolDescription = `Imports the NRSDK (XREAL SDK) into the Unity project. Can import from a local unitypackage file or download a specific version. Configures initial SDK settings after import.`;

const paramsSchema = z.object({
  source: z.enum(['local', 'url']).describe('Import source: "local" for a unitypackage file path, "url" for download URL'),
  path: z.string().optional().describe('Local file path to NRSDK .unitypackage file (required if source is "local")'),
  url: z.string().optional().describe('URL to download NRSDK from (required if source is "url")'),
  version: z.string().optional().describe('SDK version being imported (for reference)'),
  importExamples: z.boolean().default(false).describe('Import example scenes and scripts'),
  importStreamingAssets: z.boolean().default(true).describe('Import streaming assets required for device operation'),
  configurePlayerSettings: z.boolean().default(true).describe('Automatically configure Player Settings for NRSDK'),
});

/**
 * Registers the Import NRSDK tool with the MCP server.
 * This tool imports and configures the NRSDK package.
 */
export function registerImportNrsdkTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  // Validate source-specific parameters
  if (params.source === 'local' && !params.path) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Path is required when source is "local"'
    );
  }
  if (params.source === 'url' && !params.url) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'URL is required when source is "url"'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.XREAL_SDK_NOT_FOUND,
      response.message || 'Failed to import NRSDK'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
