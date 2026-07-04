import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'profiler';
const toolDescription = `Controls Unity's Profiler for performance analysis.
Actions:
- start: Begin profiling session
- stop: End profiling session
- get_frame_data: Get detailed frame timing data
- get_memory_snapshot: Capture memory usage snapshot
- get_render_stats: Get rendering statistics (draw calls, triangles, batches)
- get_cpu_usage: Get CPU time breakdown by area
- get_gc_allocs: Get garbage collection allocations
- save_report: Save profiler data to file
- clear: Clear profiler data`;

const paramsSchema = z.object({
  action: z.enum([
    'start', 'stop', 'get_frame_data', 'get_memory_snapshot',
    'get_render_stats', 'get_cpu_usage', 'get_gc_allocs',
    'save_report', 'clear', 'get_status'
  ]).describe('Profiler action to perform'),
  frameCount: z.number().min(1).max(300).optional()
    .describe('Number of frames to analyze (default: 1, max: 300)'),
  deepProfile: z.boolean().optional()
    .describe('Enable deep profiling for detailed call stacks (slower)'),
  profileGPU: z.boolean().optional()
    .describe('Include GPU profiling data'),
  profileEditor: z.boolean().optional()
    .describe('Include editor overhead in profiling'),
  memoryCategories: z.array(z.enum([
    'All', 'Native', 'Managed', 'Graphics', 'Audio', 'Video', 'Physics', 'Other'
  ])).optional().describe('Memory categories to include in snapshot'),
  savePath: z.string().optional()
    .describe('File path for saving profiler report'),
  sortBy: z.enum(['time', 'calls', 'gcAlloc', 'name']).optional()
    .describe('Sort profiler results by this metric'),
  minTime: z.number().optional()
    .describe('Minimum time in ms to include in results (filter small items)'),
  includeCallStacks: z.boolean().optional()
    .describe('Include call stacks in GC allocation data')
});

export function registerProfilerTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  if (action === 'save_report' && !params.savePath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'savePath' is required for save_report action"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      frameCount: params.frameCount || 1,
      deepProfile: params.deepProfile || false,
      profileGPU: params.profileGPU || false,
      profileEditor: params.profileEditor || false,
      memoryCategories: params.memoryCategories || ['All'],
      savePath: params.savePath,
      sortBy: params.sortBy || 'time',
      minTime: params.minTime,
      includeCallStacks: params.includeCallStacks || false
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to execute profiler action: ${action}`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
