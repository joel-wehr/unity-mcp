import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'material_shader';
const toolDescription = `Manages Unity materials and shaders.
Actions:
- get_material: Get material properties from a renderer
- set_material_property: Set a material property value
- create_material: Create a new material
- assign_material: Assign material to a renderer
- get_shader_properties: List all properties of a shader
- get_available_shaders: List available shaders
- set_shader: Change shader on a material
- copy_material: Copy material properties
- get_global_shader_property: Get global shader property
- set_global_shader_property: Set global shader property
- get_keywords: Get enabled shader keywords
- enable_keyword: Enable a shader keyword
- disable_keyword: Disable a shader keyword`;

const paramsSchema = z.object({
  action: z.enum([
    'get_material', 'set_material_property', 'create_material',
    'assign_material', 'get_shader_properties', 'get_available_shaders',
    'set_shader', 'copy_material', 'get_global_shader_property',
    'set_global_shader_property', 'get_keywords', 'enable_keyword', 'disable_keyword'
  ]).describe('Material/shader action to perform'),
  objectId: z.number().optional().describe('Instance ID of the GameObject with renderer'),
  objectPath: z.string().optional().describe('Hierarchy path of the GameObject'),
  materialIndex: z.number().optional().describe('Material index on the renderer (default: 0)'),
  materialPath: z.string().optional().describe('Asset path to material'),
  propertyName: z.string().optional().describe('Name of the material/shader property'),
  propertyType: z.enum(['Color', 'Float', 'Int', 'Vector', 'Texture', 'Matrix']).optional()
    .describe('Type of the property'),
  propertyValue: z.any().optional().describe('Value to set for the property'),
  colorValue: z.object({
    r: z.number().min(0).max(1),
    g: z.number().min(0).max(1),
    b: z.number().min(0).max(1),
    a: z.number().min(0).max(1).optional()
  }).optional().describe('Color value (0-1 range)'),
  vectorValue: z.object({
    x: z.number(), y: z.number(), z: z.number(), w: z.number().optional()
  }).optional().describe('Vector4 value'),
  texturePath: z.string().optional().describe('Asset path to texture'),
  shaderName: z.string().optional().describe('Shader name (e.g., "Standard", "Universal Render Pipeline/Lit")'),
  newMaterialName: z.string().optional().describe('Name for new material'),
  newMaterialPath: z.string().optional().describe('Path to save new material'),
  sourceMaterialPath: z.string().optional().describe('Source material to copy from'),
  keyword: z.string().optional().describe('Shader keyword name'),
  useSharedMaterial: z.boolean().optional()
    .describe('Use shared material vs instance (default: false = instance)'),
  searchFilter: z.string().optional().describe('Filter for shader search')
});

export function registerMaterialShaderTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate object target for renderer operations
  const needsObject = ['get_material', 'assign_material', 'get_keywords', 'enable_keyword', 'disable_keyword'];
  if (needsObject.includes(action) && !params.objectId && !params.objectPath && !params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Either 'objectId', 'objectPath', or 'materialPath' is required for ${action}`
    );
  }

  if (action === 'set_material_property' && !params.propertyName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'propertyName' is required for set_material_property"
    );
  }

  if (action === 'create_material' && !params.newMaterialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'newMaterialPath' is required for create_material"
    );
  }

  if (action === 'assign_material' && !params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'materialPath' is required for assign_material"
    );
  }

  if (action === 'get_shader_properties' && !params.shaderName && !params.materialPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Either 'shaderName' or 'materialPath' is required for get_shader_properties"
    );
  }

  if (action === 'set_shader' && !params.shaderName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'shaderName' is required for set_shader"
    );
  }

  if ((action === 'enable_keyword' || action === 'disable_keyword') && !params.keyword) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'keyword' is required for ${action}`
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      ...params,
      materialIndex: params.materialIndex || 0,
      useSharedMaterial: params.useSharedMaterial || false
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Material/shader action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
