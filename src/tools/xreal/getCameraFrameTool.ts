import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../../utils/toolAnnotations.js";

const toolName = 'get_camera_frame';
const toolDescription = `Captures a frame from the XREAL RGB camera for computer vision or AR development. Returns image data or saves to a file. Useful for debugging image tracking and mixed reality features.`;

const paramsSchema = z.object({
  saveToFile: z.boolean().default(true).describe('Save the captured frame to a file'),
  filePath: z.string().optional().describe('Custom file path to save the image. If not provided, saves to Assets/CapturedFrames/'),
  format: z.enum(['PNG', 'JPG', 'EXR']).default('PNG').describe('Image format for saved file'),
  resolution: z.enum(['Full', 'Half', 'Quarter']).default('Full').describe('Resolution of captured frame'),
  includeMetadata: z.boolean().default(true).describe('Include camera intrinsics and pose in response'),
  cameraType: z.enum(['RGB', 'Grayscale', 'Both']).default('RGB').describe('Type of camera data to capture'),
});

/**
 * Registers the Get Camera Frame tool with the MCP server.
 * This tool captures frames from the XREAL camera.
 */
export function registerGetCameraFrameTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      ErrorType.XREAL_DEVICE_NOT_CONNECTED,
      response.message || 'Failed to capture camera frame'
    );
  }

  let resultText = '';
  if (params.saveToFile && response.filePath) {
    resultText = `Camera frame saved to: ${response.filePath}\n`;
  }
  if (params.includeMetadata && response.metadata) {
    resultText += `\nCamera Metadata:\n${JSON.stringify(response.metadata, null, 2)}`;
  }

  return {
    content: [{
      type: 'text' as const,
      text: resultText || response.message || 'Camera frame captured successfully'
    }]
  };
}
