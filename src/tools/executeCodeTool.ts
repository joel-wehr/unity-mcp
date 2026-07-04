import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'execute_code';
const toolDescription = `Execute arbitrary C# code in the Unity Editor context. This is the most powerful tool — it can do anything the Unity Editor API allows.

The code runs with full access to UnityEngine, UnityEditor, System, and all project assemblies.

For simple expressions, just provide the expression (e.g., "Selection.activeGameObject.name").
For multi-statement code, write full C# statements. A return statement determines the output.
For void operations, the code runs and any Debug.Log output is captured.

Pre-imported namespaces: System, System.Collections.Generic, System.Linq, System.Reflection, System.IO, UnityEngine, UnityEditor, UnityEditor.SceneManagement, UnityEngine.SceneManagement, UnityEngine.UI

Examples:
- "PlayerSettings.productName" → returns product name
- "GameObject.FindObjectsOfType<Camera>().Length" → returns camera count
- "var go = new GameObject(\\"MyObject\\"); return go.name;" → creates object, returns name
- "Debug.Log(\\"Hello\\"); return Selection.objects.Length;" → logs + returns count`;

const paramsSchema = z.object({
  code: z.string().describe('C# code to execute. Can be a single expression, multiple statements, or a full class with an Execute method.')
});

export function registerExecuteCodeTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`);
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
  const { code } = params;

  if (!code || code.trim().length === 0) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'code' is required and must not be empty"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: { code }
  });

  if (!response.success) {
    const errorParts = [response.error || response.message || 'Code execution failed'];
    if (response.compilationErrors) {
      errorParts.push('\nCompilation errors:');
      for (const err of response.compilationErrors) {
        errorParts.push(`  ${err}`);
      }
    }
    if (response.consoleOutput) {
      errorParts.push(`\nConsole output:\n${response.consoleOutput}`);
    }

    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      errorParts.join('\n')
    );
  }

  const parts: string[] = [];
  if (response.result !== null && response.result !== undefined) {
    parts.push(`Result (${response.resultType}): ${response.result}`);
  }
  if (response.consoleOutput) {
    parts.push(`Console output:\n${response.consoleOutput}`);
  }
  if (response.method) {
    parts.push(`[Compiled via ${response.method}]`);
  }
  if (parts.length === 0) {
    parts.push('Code executed successfully (no return value)');
  }

  return {
    content: [{
      type: "text" as const,
      text: parts.join('\n')
    }]
  };
}
