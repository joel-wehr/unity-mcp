import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";
import { sendUnityRequestWithProgress, ProgressCapableExtra } from "../utils/progress.js";

// Constants for the tool
const toolName = 'asset_import';
const toolDescription = `Manages Unity asset import settings and reimport operations.
Actions:
- get_settings: Get import settings for an asset
- set_settings: Modify import settings
- reimport: Force reimport an asset
- reimport_all: Reimport all assets (use with caution)
- get_importer_type: Get the importer type for an asset
- apply_preset: Apply an import preset to an asset
Types: texture, model, audio, video, font, shader, plugin`;

const paramsSchema = z.object({
  action: z.enum([
    'get_settings', 'set_settings', 'reimport', 'reimport_all',
    'get_importer_type', 'apply_preset'
  ]).describe('Asset import action to perform'),
  assetPath: z.string().optional().describe('Path to the asset (e.g., "Assets/Textures/myTexture.png")'),
  assetType: z.enum([
    'texture', 'model', 'audio', 'video', 'font', 'shader', 'plugin', 'auto'
  ]).optional().describe('Type of asset for settings schema (auto-detected if not specified)'),
  textureSettings: z.object({
    maxSize: z.number().optional().describe('Max texture size (32-16384)'),
    compression: z.enum(['None', 'LowQuality', 'NormalQuality', 'HighQuality']).optional(),
    format: z.string().optional().describe('Texture format (e.g., "RGBA32", "DXT5", "ASTC_6x6")'),
    generateMipmaps: z.boolean().optional(),
    readable: z.boolean().optional(),
    sRGB: z.boolean().optional(),
    wrapMode: z.enum(['Repeat', 'Clamp', 'Mirror', 'MirrorOnce']).optional(),
    filterMode: z.enum(['Point', 'Bilinear', 'Trilinear']).optional(),
    anisoLevel: z.number().min(0).max(16).optional(),
    textureType: z.enum([
      'Default', 'NormalMap', 'Sprite', 'Cursor', 'Cookie',
      'Lightmap', 'SingleChannel'
    ]).optional(),
    spriteMode: z.enum(['Single', 'Multiple', 'Polygon']).optional(),
    pixelsPerUnit: z.number().optional()
  }).optional().describe('Texture import settings'),
  modelSettings: z.object({
    globalScale: z.number().optional().describe('Scale factor for import'),
    importBlendShapes: z.boolean().optional(),
    importVisibility: z.boolean().optional(),
    importCameras: z.boolean().optional(),
    importLights: z.boolean().optional(),
    meshCompression: z.enum(['Off', 'Low', 'Medium', 'High']).optional(),
    isReadable: z.boolean().optional(),
    optimizeMesh: z.boolean().optional(),
    generateColliders: z.boolean().optional(),
    animationType: z.enum(['None', 'Legacy', 'Generic', 'Humanoid']).optional(),
    importAnimation: z.boolean().optional(),
    importMaterials: z.boolean().optional(),
    materialLocation: z.enum(['External', 'InPrefab']).optional()
  }).optional().describe('Model (FBX/OBJ) import settings'),
  audioSettings: z.object({
    loadType: z.enum(['DecompressOnLoad', 'CompressedInMemory', 'Streaming']).optional(),
    compressionFormat: z.enum(['PCM', 'Vorbis', 'ADPCM', 'MP3']).optional(),
    quality: z.number().min(0).max(100).optional().describe('Compression quality (0-100)'),
    sampleRateSetting: z.enum(['PreserveSampleRate', 'OptimizeSampleRate', 'OverrideSampleRate']).optional(),
    sampleRateOverride: z.number().optional(),
    forceToMono: z.boolean().optional(),
    loadInBackground: z.boolean().optional(),
    preloadAudioData: z.boolean().optional()
  }).optional().describe('Audio clip import settings'),
  presetPath: z.string().optional().describe('Path to import preset to apply'),
  platform: z.enum([
    'Default', 'Standalone', 'Android', 'iOS', 'WebGL'
  ]).optional().describe('Platform-specific settings to get/set')
});

export function registerAssetImportTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate required parameters
  if (action !== 'reimport_all' && !params.assetPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'assetPath' is required for ${action}`
    );
  }

  if (action === 'apply_preset' && !params.presetPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'presetPath' is required for apply_preset"
    );
  }

  // reimport_all can be very slow on large projects; emit progress + allow cancel.
  const response = await sendUnityRequestWithProgress(
    mcpUnity,
    { method: toolName, params },
    extra,
    logger,
    { label: `Asset import: ${action}`, estimatedMs: action === 'reimport_all' ? 60000 : undefined }
  );

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Asset import action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
