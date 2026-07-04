import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";
import { sendUnityRequestWithProgress, ProgressCapableExtra } from "../utils/progress.js";

// Constants for the tool
const toolName = 'lighting';
const toolDescription = `Controls Unity lighting and rendering environment.
Actions:
- get_settings: Get current lighting settings
- set_settings: Modify lighting settings
- bake_lighting: Start lightmap baking
- cancel_bake: Cancel ongoing bake
- get_bake_status: Get baking progress
- clear_baked_data: Clear baked lighting data
- get_light_probes: Get light probe data
- update_light_probes: Refresh light probes
- set_ambient: Set ambient lighting
- set_fog: Configure fog settings
- set_skybox: Set skybox material
- get_reflection_probes: Get reflection probe data
- render_reflection_probes: Re-render reflection probes`;

const paramsSchema = z.object({
  action: z.enum([
    'get_settings', 'set_settings', 'bake_lighting', 'cancel_bake',
    'get_bake_status', 'clear_baked_data', 'get_light_probes',
    'update_light_probes', 'set_ambient', 'set_fog', 'set_skybox',
    'get_reflection_probes', 'render_reflection_probes'
  ]).describe('Lighting action to perform'),
  lightingSettings: z.object({
    realtimeGI: z.boolean().optional().describe('Enable realtime global illumination'),
    bakedGI: z.boolean().optional().describe('Enable baked global illumination'),
    mixedLighting: z.boolean().optional().describe('Enable mixed lighting'),
    shadowmaskMode: z.enum(['Shadowmask', 'DistanceShadowmask']).optional(),
    lightmapper: z.enum(['Enlighten', 'ProgressiveCPU', 'ProgressiveGPU']).optional(),
    directionalMode: z.enum(['Directional', 'NonDirectional']).optional(),
    indirectResolution: z.number().optional(),
    lightmapResolution: z.number().optional(),
    lightmapPadding: z.number().optional(),
    lightmapSize: z.number().optional(),
    compressLightmaps: z.boolean().optional(),
    aoEnabled: z.boolean().optional(),
    aoMaxDistance: z.number().optional(),
    aoIndirect: z.number().optional(),
    aoDirect: z.number().optional()
  }).optional().describe('Lighting settings to modify'),
  ambientSettings: z.object({
    mode: z.enum(['Skybox', 'Gradient', 'Color']).optional(),
    skyColor: z.object({ r: z.number(), g: z.number(), b: z.number() }).optional(),
    equatorColor: z.object({ r: z.number(), g: z.number(), b: z.number() }).optional(),
    groundColor: z.object({ r: z.number(), g: z.number(), b: z.number() }).optional(),
    ambientColor: z.object({ r: z.number(), g: z.number(), b: z.number() }).optional(),
    ambientIntensity: z.number().optional()
  }).optional().describe('Ambient lighting settings'),
  fogSettings: z.object({
    enabled: z.boolean().optional(),
    mode: z.enum(['Linear', 'Exponential', 'ExponentialSquared']).optional(),
    color: z.object({ r: z.number(), g: z.number(), b: z.number() }).optional(),
    density: z.number().optional(),
    startDistance: z.number().optional(),
    endDistance: z.number().optional()
  }).optional().describe('Fog settings'),
  skyboxPath: z.string().optional().describe('Asset path to skybox material'),
  bakeOptions: z.object({
    async: z.boolean().optional().describe('Bake asynchronously'),
    generateLightProbes: z.boolean().optional()
  }).optional().describe('Options for baking'),
  reflectionProbeId: z.number().optional().describe('Specific reflection probe instance ID')
});

export function registerLightingTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any, extra: ProgressCapableExtra) => {
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

async function toolHandler(mcpUnity: McpUnity, params: any, logger: Logger, extra?: ProgressCapableExtra): Promise<CallToolResult> {
  const { action } = params;

  if (action === 'set_settings' && !params.lightingSettings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'lightingSettings' is required for set_settings"
    );
  }

  if (action === 'set_ambient' && !params.ambientSettings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'ambientSettings' is required for set_ambient"
    );
  }

  if (action === 'set_fog' && !params.fogSettings) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'fogSettings' is required for set_fog"
    );
  }

  if (action === 'set_skybox' && !params.skyboxPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'skyboxPath' is required for set_skybox"
    );
  }

  // Baking (bake / bake_reflection_probes) can take minutes; emit progress +
  // forward cancellation. Light query/config actions return before the first tick.
  const bakingActions = ['bake', 'bake_reflection_probes', 'bake_all'];
  const response = await sendUnityRequestWithProgress(
    mcpUnity,
    { method: toolName, params },
    extra,
    logger,
    { label: `Lighting: ${action}`, estimatedMs: bakingActions.includes(action) ? 120000 : undefined }
  );

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Lighting action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
