import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'profile_xr_scene';
const toolDescription = `Analyzes the current scene for XR performance issues. Checks draw calls, triangle counts, texture memory, shader complexity, and provides optimization recommendations.`;

const paramsSchema = z.object({
  analyzeRendering: z.boolean().default(true).describe('Analyze rendering performance (draw calls, batching, overdraw)'),
  analyzePhysics: z.boolean().default(true).describe('Analyze physics complexity'),
  analyzeScripts: z.boolean().default(true).describe('Analyze script performance impact'),
  analyzeMemory: z.boolean().default(true).describe('Analyze memory usage and allocations'),
  analyzeAssets: z.boolean().default(true).describe('Analyze asset sizes and compression'),
  targetFrameRate: z.number().default(72).describe('Target frame rate for recommendations (XREAL typical: 60-90)'),
  generateReport: z.boolean().default(true).describe('Generate detailed report with recommendations'),
  highlightIssues: z.boolean().default(true).describe('Highlight problematic objects in Scene view'),
});

/**
 * Registers the Profile XR Scene tool with the MCP server.
 * This tool analyzes scene performance.
 */
export function registerProfileXrSceneTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to profile XR scene'
    );
  }

  // Format the report
  const report = response.report || {};
  let reportText = '# XR Scene Performance Analysis\n\n';

  reportText += `## Summary\n`;
  reportText += `- Target Frame Rate: ${params.targetFrameRate} FPS\n`;
  reportText += `- Overall Status: ${report.status || 'N/A'}\n\n`;

  if (report.rendering) {
    reportText += `## Rendering\n`;
    reportText += `- Draw Calls: ${report.rendering.drawCalls || 'N/A'}\n`;
    reportText += `- Triangles: ${report.rendering.triangles || 'N/A'}\n`;
    reportText += `- Batches: ${report.rendering.batches || 'N/A'}\n\n`;
  }

  if (report.recommendations && report.recommendations.length > 0) {
    reportText += `## Recommendations\n`;
    report.recommendations.forEach((rec: string, i: number) => {
      reportText += `${i + 1}. ${rec}\n`;
    });
  }

  return {
    content: [{
      type: 'text' as const,
      text: params.generateReport ? reportText : JSON.stringify(response, null, 2)
    }]
  };
}
