import * as z from 'zod';
import { McpUnity } from '../../unity/mcpUnity.js';
import { Logger } from '../../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'get_build_status';
const toolDescription = `Returns the current status of an asynchronous build started by build_xreal_apk.
Provide the jobId returned from that tool. State transitions: Queued -> Building -> Succeeded | Failed.
When state is Succeeded the response includes outputPath, fileSizeMb, buildSeconds, and (if runAfterBuild
was set) a deployment summary. When state is Failed the response includes an errors array.
Omit jobId to get a list of every known build job in this editor session.`;

const paramsSchema = z.object({
  jobId: z.string().optional().describe('The jobId returned from build_xreal_apk. Omit to list all jobs.'),
});

/**
 * Registers the Get Build Status tool with the MCP server.
 * This tool polls the status of an asynchronous APK build.
 */
export function registerGetBuildStatusTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  if (response && response.success === false) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.error || response.message || 'Failed to get build status'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
