import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";
import { sendUnityRequestWithProgress, ProgressCapableExtra } from "../utils/progress.js";

// Constants for the tool
const toolName = 'run_tests';
const toolDescription = 'Runs Unity\'s Test Runner tests';
const paramsSchema = z.object({
  testMode: z.string().optional().default('EditMode').describe('The test mode to run (EditMode or PlayMode) - defaults to EditMode (optional)'),
  testFilter: z.string().optional().default('').describe('The specific test filter to run (e.g. specific test name or class name, must include namespace) (optional)'),
  returnOnlyFailures: z.boolean().optional().default(true).describe('Whether to show only failed tests in the results (optional)'),
  returnWithLogs: z.boolean().optional().default(false).describe('Whether to return the test logs in the results (optional)')
});

// Structured output schema (permissive: fields optional so real Unity responses
// never fail validation; extra keys are tolerated by the SDK).
const outputSchema = {
  success: z.boolean().optional().describe('Whether the test run completed'),
  message: z.string().optional().describe('Human-readable summary or error'),
  testCount: z.number().optional().describe('Total tests executed'),
  passCount: z.number().optional().describe('Number of passing tests'),
  failCount: z.number().optional().describe('Number of failing tests'),
  skipCount: z.number().optional().describe('Number of skipped tests'),
  results: z.array(z.record(z.any())).optional().describe('Per-test results')
};

/**
 * Creates and registers the Run Tests tool with the MCP server
 * This tool allows running tests in the Unity Test Runner
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerRunTestsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      outputSchema,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any = {}, extra: ProgressCapableExtra) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params, logger, extra);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

/**
 * Handles running tests in Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(mcpUnity: McpUnity, params: any = {}, logger: Logger, extra?: ProgressCapableExtra): Promise<CallToolResult> {
  const {
    testMode = 'EditMode',
    testFilter = '',
    returnOnlyFailures = true,
    returnWithLogs = false
  } = params;

  // Create and wait for the test run (with cancellation + progress heartbeats)
  const response = await sendUnityRequestWithProgress(
    mcpUnity,
    {
      method: toolName,
      params: {
        testMode,
        testFilter,
        returnOnlyFailures,
        returnWithLogs
      }
    },
    extra,
    logger,
    { label: `Running ${testMode} tests`, estimatedMs: 60000 }
  );

  // Process the test results
  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to run tests: Mode=${testMode}, Filter=${testFilter || 'none'}`
    );
  }

  // Extract test results
  const testResults = response.results || [];
  const testCount = response.testCount || 0;
  const passCount = response.passCount || 0;
  const failCount = response.failCount || 0;
  const skipCount = response.skipCount || 0;

  return {
    content: [
      {
        type: 'text',
        text: JSON.stringify(response, null, 2)
      },
      {
        type: 'text',
        text: JSON.stringify({
          testCount,
          passCount,
          failCount,
          skipCount,
          results: testResults
        }, null, 2)
      }
    ],
    structuredContent: {
      success: response.success ?? true,
      message: response.message,
      testCount,
      passCount,
      failCount,
      skipCount,
      results: testResults
    }
  };
}
