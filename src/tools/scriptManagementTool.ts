import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'script_management';
const toolDescription = `Manages Unity scripting configuration including:
- Scripting Define Symbols (#define directives)
- Assembly Definitions (asmdef files)
- Script Execution Order
- Script compilation state
Actions: get_defines, set_defines, add_define, remove_define, get_assemblies, create_assembly, get_execution_order, set_execution_order, get_compilation_state`;

const paramsSchema = z.object({
  action: z.enum([
    'get_defines', 'set_defines', 'add_define', 'remove_define',
    'get_assemblies', 'create_assembly', 'modify_assembly',
    'get_execution_order', 'set_execution_order',
    'get_compilation_state'
  ]).describe('The script management action to perform'),
  platform: z.enum([
    'Standalone', 'Android', 'iOS', 'WebGL', 'Server'
  ]).optional().describe('Target platform for define symbols (defaults to active build target)'),
  defines: z.array(z.string()).optional().describe('Array of define symbols for set_defines'),
  define: z.string().optional().describe('Single define symbol for add_define/remove_define'),
  assemblyName: z.string().optional().describe('Name for the assembly definition'),
  assemblyPath: z.string().optional().describe('Path for the assembly definition file'),
  assemblySettings: z.object({
    references: z.array(z.string()).optional(),
    includePlatforms: z.array(z.string()).optional(),
    excludePlatforms: z.array(z.string()).optional(),
    allowUnsafeCode: z.boolean().optional(),
    autoReferenced: z.boolean().optional(),
    noEngineReferences: z.boolean().optional(),
    defineConstraints: z.array(z.string()).optional(),
    versionDefines: z.array(z.object({
      name: z.string(),
      expression: z.string(),
      define: z.string()
    })).optional()
  }).optional().describe('Settings for assembly definition'),
  executionOrder: z.array(z.object({
    scriptName: z.string().describe('Full script class name including namespace'),
    order: z.number().describe('Execution order value (negative = earlier, positive = later)')
  })).optional().describe('Script execution order entries')
});

export function registerScriptManagementTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate required parameters for specific actions
  if ((action === 'add_define' || action === 'remove_define') && !params.define) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'define' is required for ${action}`
    );
  }

  if (action === 'set_defines' && !params.defines) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'defines' is required for set_defines"
    );
  }

  if (action === 'create_assembly' && (!params.assemblyName || !params.assemblyPath)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameters 'assemblyName' and 'assemblyPath' are required for create_assembly"
    );
  }

  if (action === 'set_execution_order' && !params.executionOrder) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'executionOrder' is required for set_execution_order"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to execute ${action}`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
