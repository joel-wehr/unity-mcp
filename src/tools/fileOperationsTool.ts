import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'file_operations';
const toolDescription = `Read, write, list, and search files within the Unity project.

Actions:
- read: Read the contents of a file (scripts, shaders, configs, etc.)
- write: Write/create a file in the project (auto-imports to AssetDatabase)
- list: List files in a directory with optional pattern filtering
- exists: Check if a file or directory exists
- search: Search file contents for a text query (grep-like)
- get_script_classes: Analyze a C# script to get its classes, methods, and fields

Paths can be absolute or relative to the project root. Examples:
- "Assets/Scripts/MyScript.cs" (relative)
- "Assets/Shaders/" (directory)
- "ProjectSettings/ProjectSettings.asset" (project settings)

Security: All paths must be within the Unity project directory.`;

const paramsSchema = z.object({
  action: z.enum(['read', 'write', 'list', 'exists', 'search', 'get_script_classes'])
    .describe('File operation to perform'),
  path: z.string().optional()
    .describe('File or directory path (relative to project root or absolute). Examples: "Assets/Scripts/MyScript.cs", "Assets/"'),
  content: z.string().optional()
    .describe('File content to write (required for write action)'),
  pattern: z.string().optional()
    .describe('File name pattern for list action (e.g., "*.cs", "*.shader"). Default: "*.*"'),
  recursive: z.boolean().optional()
    .describe('Recurse into subdirectories for list action. Default: false'),
  query: z.string().optional()
    .describe('Search text for search action'),
  extension: z.string().optional()
    .describe('File extension filter for search action (e.g., ".cs", ".shader"). Default: ".cs"')
});

export function registerFileOperationsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  if (action === 'write' && !params.content && params.content !== '') {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'content' is required for write action"
    );
  }

  if (action === 'search' && !params.query) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'query' is required for search action"
    );
  }

  if (['read', 'write', 'exists', 'get_script_classes'].includes(action) && !params.path) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'path' is required for ${action} action`
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || response.error || `File operation '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
