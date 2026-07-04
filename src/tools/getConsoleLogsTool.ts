import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = "get_console_logs";
const toolDescription = "Retrieves logs from the Unity console with pagination support to avoid token limits";
const paramsSchema = z.object({
  logType: z
    .enum(["info", "warning", "error"])
    .optional()
    .describe(
      "The type of logs to retrieve (info, warning, error) - defaults to all logs if not specified"
    ),
  offset: z
    .number()
    .int()
    .min(0)
    .optional()
    .describe("Starting index for pagination (0-based, defaults to 0)"),
  limit: z
    .number()
    .int()
    .min(1)
    .max(500)
    .optional()
    .describe("Maximum number of logs to return (defaults to 50, max 500 to avoid token limits)"),
  includeStackTrace: z
    .boolean()
    .optional()
    .describe("Whether to include stack trace in logs. Set to false to save 80-90% tokens. Default: true")
});

// Structured output schema (permissive: log entry shape + pagination fields all
// optional, extra keys tolerated).
const outputSchema = {
  success: z.boolean().optional().describe('Whether the logs were retrieved'),
  message: z.string().optional().describe('Human-readable summary or error'),
  logs: z.array(z.object({
    message: z.string().optional(),
    type: z.string().optional(),
    stackTrace: z.string().optional(),
  }).passthrough()).optional().describe('Console log entries'),
  offset: z.number().optional().describe('Pagination offset used'),
  limit: z.number().optional().describe('Pagination limit used'),
  total: z.number().optional().describe('Total number of matching logs available'),
};

/**
 * Creates and registers the Get Console Logs tool with the MCP server
 * This tool allows retrieving messages from the Unity console
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerGetConsoleLogsTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
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

/**
 * Handles requests for Unity console logs
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const { logType, offset = 0, limit = 50, includeStackTrace = true } = params;

  // Send request to Unity using the same method name as the resource
  const response = await mcpUnity.sendRequest({
    method: "get_console_logs",
    params: {
      logType: logType,
      offset: offset,
      limit: limit,
      includeStackTrace: includeStackTrace,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to fetch logs from Unity"
    );
  }

  return {
    content: [
      {
        type: "text" as const,
        text: JSON.stringify(response, null, 2)
      },
    ],
    structuredContent: response,
  };
}
