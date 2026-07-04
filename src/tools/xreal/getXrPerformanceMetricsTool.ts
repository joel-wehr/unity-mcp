import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_xr_performance_metrics';
const toolDescription = `Gets real-time XR performance metrics including frame rate, frame times, GPU/CPU usage, thermal state, and memory usage. Essential for optimizing mobile XR experiences.`;

const paramsSchema = z.object({
  includeFrameMetrics: z.boolean().default(true).describe('Include FPS, frame times, dropped frames'),
  includeGpuMetrics: z.boolean().default(true).describe('Include GPU utilization and timing'),
  includeCpuMetrics: z.boolean().default(true).describe('Include CPU utilization by thread'),
  includeMemoryMetrics: z.boolean().default(true).describe('Include memory usage statistics'),
  includeThermalState: z.boolean().default(true).describe('Include device thermal throttling state'),
  includeTrackingMetrics: z.boolean().default(true).describe('Include tracking quality metrics'),
  averageOverFrames: z.number().int().min(1).max(120).default(30).describe('Number of frames to average metrics over'),
});

/**
 * Registers the Get XR Performance Metrics tool with the MCP server.
 * This tool retrieves XR-specific performance data.
 */
export function registerGetXrPerformanceMetricsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
      response.message || 'Failed to get XR performance metrics'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response.metrics || response, null, 2)
    }]
  };
}
