import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'capture_xr_screenshot';
const toolDescription = `Captures a screenshot from the XR camera perspective. Can capture mono, stereo (side-by-side), or individual eye views. Useful for documentation and debugging.`;

const paramsSchema = z.object({
  outputPath: z.string().optional().describe('Path to save the screenshot. Defaults to Assets/Screenshots/'),
  fileName: z.string().optional().describe('Custom file name. Defaults to timestamp-based name'),
  captureMode: z.enum(['Mono', 'Stereo', 'LeftEye', 'RightEye', 'Both']).default('Mono').describe('Eye view(s) to capture'),
  resolution: z.enum(['Native', '1080p', '2K', '4K', 'Custom']).default('Native').describe('Screenshot resolution'),
  customWidth: z.number().optional().describe('Custom width in pixels (if resolution is Custom)'),
  customHeight: z.number().optional().describe('Custom height in pixels (if resolution is Custom)'),
  superSampling: z.number().min(1).max(4).default(1).describe('Super sampling multiplier for higher quality'),
  format: z.enum(['PNG', 'JPG', 'EXR']).default('PNG').describe('Image format'),
  jpgQuality: z.number().min(1).max(100).default(95).describe('JPEG quality (if format is JPG)'),
  includeUI: z.boolean().default(true).describe('Include UI elements in screenshot'),
  transparentBackground: z.boolean().default(false).describe('Use transparent background (PNG only)'),
});

/**
 * Registers the Capture XR Screenshot tool with the MCP server.
 * This tool captures XR viewport screenshots.
 */
export function registerCaptureXrScreenshotTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  // Validate custom resolution
  if (params.resolution === 'Custom' && (!params.customWidth || !params.customHeight)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'customWidth and customHeight are required when resolution is Custom'
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to capture XR screenshot'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
