import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'debugger';
const toolDescription = `Unity debugging utilities for inspection and troubleshooting.
Actions:
- evaluate_expression: Evaluate a C# expression in the current context
- get_stack_trace: Get current stack trace (when paused or from last exception)
- list_breakpoints: List all breakpoints (requires external debugger)
- dump_object: Get detailed dump of an object's fields and properties
- invoke_method: Invoke a method on a GameObject or component
- get_static_field: Get value of a static field from a type
- set_static_field: Set value of a static field
- call_static_method: Call a static method on a type
- debug_log: Log structured debug information
- get_component_values: Get all serialized values from a component`;

const paramsSchema = z.object({
  action: z.enum([
    'evaluate_expression', 'get_stack_trace', 'list_breakpoints',
    'dump_object', 'invoke_method', 'get_static_field', 'set_static_field',
    'call_static_method', 'debug_log', 'get_component_values'
  ]).describe('Debugger action to perform'),
  expression: z.string().optional()
    .describe('C# expression to evaluate'),
  objectId: z.number().optional()
    .describe('Instance ID of the object'),
  objectPath: z.string().optional()
    .describe('Hierarchy path of the object'),
  componentType: z.string().optional()
    .describe('Component type name (e.g., "Transform", "MyScript")'),
  methodName: z.string().optional()
    .describe('Method name to invoke'),
  methodArgs: z.array(z.any()).optional()
    .describe('Arguments for method invocation'),
  typeName: z.string().optional()
    .describe('Full type name including namespace (e.g., "UnityEngine.Application")'),
  fieldName: z.string().optional()
    .describe('Static field name'),
  fieldValue: z.any().optional()
    .describe('Value to set for static field'),
  maxDepth: z.number().min(1).max(10).optional()
    .describe('Maximum depth for object dump (default: 3)'),
  includePrivate: z.boolean().optional()
    .describe('Include private fields in dump'),
  debugData: z.record(z.any()).optional()
    .describe('Structured data for debug_log')
});

export function registerDebuggerTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate required parameters based on action
  if (action === 'evaluate_expression' && !params.expression) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'expression' is required for evaluate_expression"
    );
  }

  if (action === 'dump_object' && !params.objectId && !params.objectPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'objectId' or 'objectPath' is required for dump_object"
    );
  }

  if (action === 'invoke_method' && !params.methodName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'methodName' is required for invoke_method"
    );
  }

  if (['get_static_field', 'set_static_field', 'call_static_method'].includes(action) && !params.typeName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'typeName' is required for ${action}`
    );
  }

  if (action === 'get_static_field' && !params.fieldName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'fieldName' is required for get_static_field"
    );
  }

  if (action === 'set_static_field' && (!params.fieldName || params.fieldValue === undefined)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameters 'fieldName' and 'fieldValue' are required for set_static_field"
    );
  }

  if (action === 'call_static_method' && !params.methodName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'methodName' is required for call_static_method"
    );
  }

  if (action === 'get_component_values' && !params.componentType) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'componentType' is required for get_component_values"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      expression: params.expression,
      objectId: params.objectId,
      objectPath: params.objectPath,
      componentType: params.componentType,
      methodName: params.methodName,
      methodArgs: params.methodArgs || [],
      typeName: params.typeName,
      fieldName: params.fieldName,
      fieldValue: params.fieldValue,
      maxDepth: params.maxDepth || 3,
      includePrivate: params.includePrivate || false,
      debugData: params.debugData
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Debugger action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
