import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'watch_console';
const toolDescription = `Monitors the Unity console for new logs with filtering and waiting capabilities.
Perfect for recursive iteration - wait for specific messages, errors, or compilation results.
Actions:
- wait_for_message: Block until a message matching pattern appears (with timeout)
- wait_for_error: Block until an error occurs (with timeout)
- wait_for_silence: Block until no new logs for specified duration
- wait_for_compilation: Block until scripts finish compiling
- wait_for_play_mode: Block until play mode state changes
- get_new_logs: Get logs since last check (uses internal cursor)
- reset_cursor: Reset the log cursor to current position`;

const paramsSchema = z.object({
  action: z.enum([
    'wait_for_message', 'wait_for_error', 'wait_for_silence',
    'wait_for_compilation', 'wait_for_play_mode', 'get_new_logs', 'reset_cursor'
  ]).describe('Watch action to perform'),
  pattern: z.string().optional()
    .describe('Regex pattern to match in log messages'),
  logType: z.enum(['info', 'warning', 'error', 'all']).optional()
    .describe('Type of logs to watch (default: all)'),
  timeout: z.number().min(100).max(300000).optional()
    .describe('Maximum time to wait in milliseconds (default: 30000, max: 300000)'),
  silenceDuration: z.number().min(100).max(60000).optional()
    .describe('Duration of silence to wait for in milliseconds (default: 1000)'),
  targetState: z.enum(['Playing', 'Paused', 'Stopped']).optional()
    .describe('Target play mode state to wait for'),
  includeStackTrace: z.boolean().optional()
    .describe('Include stack traces in returned logs'),
  maxLogs: z.number().min(1).max(500).optional()
    .describe('Maximum number of logs to return (default: 100)')
});

export function registerWatchConsoleTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
  if (action === 'wait_for_message' && !params.pattern) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'pattern' is required for wait_for_message"
    );
  }

  if (action === 'wait_for_play_mode' && !params.targetState) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'targetState' is required for wait_for_play_mode"
    );
  }

  // Set appropriate timeout based on action
  let requestTimeout = params.timeout || 30000;
  if (action === 'wait_for_compilation') {
    requestTimeout = Math.max(requestTimeout, 60000); // Min 60s for compilation
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      pattern: params.pattern,
      logType: params.logType || 'all',
      timeout: params.timeout || 30000,
      silenceDuration: params.silenceDuration || 1000,
      targetState: params.targetState,
      includeStackTrace: params.includeStackTrace || false,
      maxLogs: params.maxLogs || 100
    }
  });

  if (!response.success) {
    // Check for timeout specifically
    if (response.message?.includes('timeout') || response.message?.includes('Timeout')) {
      throw new McpUnityError(
        ErrorType.TIMEOUT,
        response.message || `Watch action '${action}' timed out`
      );
    }
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Watch action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
