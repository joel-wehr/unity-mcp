// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';

// Import tools
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerAddExternalDllTool } from './tools/addExternalDllTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerDeleteGameObjectTool } from './tools/deleteGameObjectTool.js';
import { registerDuplicateGameObjectTool } from './tools/duplicateGameObjectTool.js';
import { registerPlayModeTool } from './tools/playModeTool.js';
import { registerAssetManagementTool } from './tools/assetManagementTool.js';
import { registerFindGameObjectsTool } from './tools/findGameObjectsTool.js';
import { registerEditorSelectionTool } from './tools/editorSelectionTool.js';
import { registerSearchUnityKnowledgeTool, configureRagServer } from './tools/searchUnityKnowledgeTool.js';

// Import new comprehensive control tools
import { registerProjectSettingsTool } from './tools/projectSettingsTool.js';
import { registerScriptManagementTool } from './tools/scriptManagementTool.js';
import { registerProfilerTool } from './tools/profilerTool.js';
import { registerBuildPipelineTool } from './tools/buildPipelineTool.js';
import { registerEditorControlTool } from './tools/editorControlTool.js';
import { registerUndoRedoTool } from './tools/undoRedoTool.js';
import { registerWatchConsoleTool } from './tools/watchConsoleTool.js';
import { registerDebuggerTool } from './tools/debuggerTool.js';
import { registerAssetImportTool } from './tools/assetImportTool.js';
import { registerAnimationTool } from './tools/animationTool.js';
import { registerPhysicsTool } from './tools/physicsTool.js';
import { registerMaterialShaderTool } from './tools/materialShaderTool.js';
import { registerLightingTool } from './tools/lightingTool.js';
import { registerUnityHubTool } from './tools/unityHubTool.js';
import { registerAssetStoreTool } from './tools/assetStoreTool.js';
import { registerExecuteCodeTool } from './tools/executeCodeTool.js';
import { registerFileOperationsTool } from './tools/fileOperationsTool.js';
import { registerScriptableObjectTool } from './tools/scriptableObjectTool.js';
import { registerPrefabTool } from './tools/prefabTool.js';
import { registerAudioMixerTool } from './tools/audioMixerTool.js';
import { registerTerrainTool } from './tools/terrainTool.js';
import { registerNavmeshTool } from './tools/navmeshTool.js';
import { registerPhysics2dTool } from './tools/physics2dTool.js';
import { registerTilemapTool } from './tools/tilemapTool.js';
import { registerSpriteTool } from './tools/spriteTool.js';
import { registerParticleSystemTool } from './tools/particleSystemTool.js';
import { registerPlaytestTool } from './tools/playtestTool.js';

// Import XREAL tools - Project Setup
import { registerSetupXrealProjectTool } from './tools/xreal/setupXrealProjectTool.js';
import { registerConfigureAndroidBuildTool } from './tools/xreal/configureAndroidBuildTool.js';
import { registerImportNrsdkTool } from './tools/xreal/importNrsdkTool.js';
import { registerValidateXrealSetupTool } from './tools/xreal/validateXrealSetupTool.js';

// Import XREAL tools - Device
import { registerGetXrealDeviceInfoTool } from './tools/xreal/getXrealDeviceInfoTool.js';
import { registerSetTrackingModeTool } from './tools/xreal/setTrackingModeTool.js';
import { registerCalibrateGlassesTool } from './tools/xreal/calibrateGlassesTool.js';
import { registerGetCameraFrameTool } from './tools/xreal/getCameraFrameTool.js';

// Import XREAL tools - Hand Tracking
import { registerEnableHandTrackingTool } from './tools/xreal/enableHandTrackingTool.js';
import { registerGetHandStateTool } from './tools/xreal/getHandStateTool.js';
import { registerConfigureHandGesturesTool } from './tools/xreal/configureHandGesturesTool.js';
import { registerCreateHandInteractableTool } from './tools/xreal/createHandInteractableTool.js';

// Import XREAL tools - Spatial Mapping
import { registerEnablePlaneDetectionTool } from './tools/xreal/enablePlaneDetectionTool.js';
import { registerGetDetectedPlanesTool } from './tools/xreal/getDetectedPlanesTool.js';
import { registerCreateSpatialAnchorTool } from './tools/xreal/createSpatialAnchorTool.js';
import { registerManageSpatialAnchorsTool } from './tools/xreal/manageSpatialAnchorsTool.js';
import { registerEnableMeshingTool } from './tools/xreal/enableMeshingTool.js';

// Import XREAL tools - Image Tracking
import { registerAddTrackingImageTool } from './tools/xreal/addTrackingImageTool.js';
import { registerConfigureImageTrackingTool } from './tools/xreal/configureImageTrackingTool.js';
import { registerGetTrackedImagesTool } from './tools/xreal/getTrackedImagesTool.js';

// Import XREAL tools - Mixed Reality
import { registerConfigurePassthroughTool } from './tools/xreal/configurePassthroughTool.js';
import { registerSetRenderModeTool } from './tools/xreal/setRenderModeTool.js';
import { registerConfigureOcclusionTool } from './tools/xreal/configureOcclusionTool.js';

// Import XREAL tools - Build
import { registerBuildXrealApkTool } from './tools/xreal/buildXrealApkTool.js';
import { registerGetBuildStatusTool } from './tools/xreal/getBuildStatusTool.js';
import { registerGetConnectedDevicesTool } from './tools/xreal/getConnectedDevicesTool.js';

// Import XREAL tools - XR Interaction
import { registerSetupXrInteractionTool } from './tools/xreal/setupXrInteractionTool.js';
import { registerCreateXrRigTool } from './tools/xreal/createXrRigTool.js';
import { registerAddXrInteractorTool } from './tools/xreal/addXrInteractorTool.js';
import { registerCreateXrUiTool } from './tools/xreal/createXrUiTool.js';

// Import XREAL tools - Performance
import { registerGetXrPerformanceMetricsTool } from './tools/xreal/getXrPerformanceMetricsTool.js';
import { registerProfileXrSceneTool } from './tools/xreal/profileXrSceneTool.js';
import { registerCaptureXrScreenshotTool } from './tools/xreal/captureXrScreenshotTool.js';

// Import resources
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';

// Import new comprehensive control resources
import { registerGetProfilerDataResource } from './resources/getProfilerDataResource.js';
import { registerGetBuildStatusResource } from './resources/getBuildStatusResource.js';
import { registerGetEditorStateResource } from './resources/getEditorStateResource.js';
import { registerGetProjectSettingsResource } from './resources/getProjectSettingsResource.js';

// Import XREAL resources
import { registerGetDeviceStateResource } from './resources/xreal/getDeviceStateResource.js';
import { registerGetHandTrackingResource } from './resources/xreal/getHandTrackingResource.js';
import { registerGetSpatialAnchorsResource } from './resources/xreal/getSpatialAnchorsResource.js';
import { registerGetDetectedPlanesResource } from './resources/xreal/getDetectedPlanesResource.js';
import { registerGetTrackedImagesResource } from './resources/xreal/getTrackedImagesResource.js';
import { registerGetBuildSettingsResource } from './resources/xreal/getBuildSettingsResource.js';

// Import prompts
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';
import { registerScriptWorkflowPrompt } from './prompts/scriptWorkflowPrompt.js';
import { registerSceneSetupPrompt } from './prompts/sceneSetupPrompt.js';
import { registerDebuggingWorkflowPrompt } from './prompts/debuggingWorkflowPrompt.js';
import { registerPerformanceOptimizationPrompt } from './prompts/performanceOptimizationPrompt.js';
import { registerUiDevelopmentPrompt } from './prompts/uiDevelopmentPrompt.js';
import { register2dGameDevPrompt } from './prompts/twoDGameDevPrompt.js';
import { registerWorldBuildingPrompt } from './prompts/worldBuildingPrompt.js';
import { registerAudioDesignPrompt } from './prompts/audioDesignPrompt.js';

// Import XREAL prompts
import { registerXrealProjectSetupPrompt } from './prompts/xreal/xrealProjectSetupPrompt.js';
import { registerHandInteractionPrompt } from './prompts/xreal/handInteractionPrompt.js';
import { registerSpatialAnchorWorkflowPrompt } from './prompts/xreal/spatialAnchorWorkflowPrompt.js';
import { registerXrealOptimizationPrompt } from './prompts/xreal/xrealOptimizationPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer(
  {
    name: "Unity MCP Server",
    version: "1.0.0"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all Unity Editor tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerAddExternalDllTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerPlayModeTool(server, mcpUnity, toolLogger);
registerAssetManagementTool(server, mcpUnity, toolLogger);
registerFindGameObjectsTool(server, mcpUnity, toolLogger);
registerEditorSelectionTool(server, mcpUnity, toolLogger);

// Register RAG knowledge search tool
registerSearchUnityKnowledgeTool(server, toolLogger);

// Register comprehensive control tools
registerProjectSettingsTool(server, mcpUnity, toolLogger);
registerScriptManagementTool(server, mcpUnity, toolLogger);
registerProfilerTool(server, mcpUnity, toolLogger);
registerBuildPipelineTool(server, mcpUnity, toolLogger);
registerEditorControlTool(server, mcpUnity, toolLogger);
registerUndoRedoTool(server, mcpUnity, toolLogger);
registerWatchConsoleTool(server, mcpUnity, toolLogger);
registerDebuggerTool(server, mcpUnity, toolLogger);
registerAssetImportTool(server, mcpUnity, toolLogger);
registerAnimationTool(server, mcpUnity, toolLogger);
registerPhysicsTool(server, mcpUnity, toolLogger);
registerMaterialShaderTool(server, mcpUnity, toolLogger);
registerLightingTool(server, mcpUnity, toolLogger);

// Register Unity Hub tool (independent of Unity Editor connection)
registerUnityHubTool(server, toolLogger);

// Register Asset Store tool
registerAssetStoreTool(server, mcpUnity, toolLogger);

// Register code execution tool
registerExecuteCodeTool(server, mcpUnity, toolLogger);

// Register file operations tool
registerFileOperationsTool(server, mcpUnity, toolLogger);

// Register new system tools
registerScriptableObjectTool(server, mcpUnity, toolLogger);
registerPrefabTool(server, mcpUnity, toolLogger);
registerAudioMixerTool(server, mcpUnity, toolLogger);
registerTerrainTool(server, mcpUnity, toolLogger);
registerNavmeshTool(server, mcpUnity, toolLogger);
registerPhysics2dTool(server, mcpUnity, toolLogger);
registerTilemapTool(server, mcpUnity, toolLogger);
registerSpriteTool(server, mcpUnity, toolLogger);
registerParticleSystemTool(server, mcpUnity, toolLogger);
registerPlaytestTool(server, mcpUnity, toolLogger);

// Register restart_server tool — allows AI to rebuild and restart the MCP server
import { execSync } from 'child_process';
import * as z from 'zod';
server.tool(
  'restart_server',
  'Rebuild TypeScript and restart the MCP server to pick up code changes. Use after modifying TS tools, C# handlers, or prompts. The server will exit and be auto-restarted by the host (Claude Code).',
  { rebuild: z.boolean().optional().describe('Run npm build before restarting (default: true)') },
  async (params: any) => {
    const shouldBuild = params.rebuild !== false;
    const serverDir = import.meta.dirname ? import.meta.dirname.replace(/[\\/]build$/, '') : process.cwd();
    let buildOutput = '';

    if (shouldBuild) {
      try {
        buildOutput = execSync('npm run build', { cwd: serverDir, encoding: 'utf-8', timeout: 30000 });
      } catch (err: any) {
        return {
          content: [{ type: 'text' as const, text: `Build failed:\n${err.stderr || err.message}` }]
        };
      }
    }

    // Schedule exit after response is sent
    setTimeout(() => process.exit(0), 500);

    return {
      content: [{ type: 'text' as const, text: `${shouldBuild ? 'Build succeeded. ' : ''}Server restarting...` }]
    };
  }
);

// Configure RAG server if environment variables are set
if (process.env.RAG_PYTHON_PATH && process.env.RAG_SERVER_PATH) {
  configureRagServer({
    pythonPath: process.env.RAG_PYTHON_PATH,
    serverPath: process.env.RAG_SERVER_PATH,
    dbPath: process.env.RAG_DB_PATH || ''
  });
  serverLogger.info('RAG server configured');
}

// Register XREAL tools - Project Setup
registerSetupXrealProjectTool(server, mcpUnity, toolLogger);
registerConfigureAndroidBuildTool(server, mcpUnity, toolLogger);
registerImportNrsdkTool(server, mcpUnity, toolLogger);
registerValidateXrealSetupTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Device
registerGetXrealDeviceInfoTool(server, mcpUnity, toolLogger);
registerSetTrackingModeTool(server, mcpUnity, toolLogger);
registerCalibrateGlassesTool(server, mcpUnity, toolLogger);
registerGetCameraFrameTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Hand Tracking
registerEnableHandTrackingTool(server, mcpUnity, toolLogger);
registerGetHandStateTool(server, mcpUnity, toolLogger);
registerConfigureHandGesturesTool(server, mcpUnity, toolLogger);
registerCreateHandInteractableTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Spatial Mapping
registerEnablePlaneDetectionTool(server, mcpUnity, toolLogger);
registerGetDetectedPlanesTool(server, mcpUnity, toolLogger);
registerCreateSpatialAnchorTool(server, mcpUnity, toolLogger);
registerManageSpatialAnchorsTool(server, mcpUnity, toolLogger);
registerEnableMeshingTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Image Tracking
registerAddTrackingImageTool(server, mcpUnity, toolLogger);
registerConfigureImageTrackingTool(server, mcpUnity, toolLogger);
registerGetTrackedImagesTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Mixed Reality
registerConfigurePassthroughTool(server, mcpUnity, toolLogger);
registerSetRenderModeTool(server, mcpUnity, toolLogger);
registerConfigureOcclusionTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Build
registerBuildXrealApkTool(server, mcpUnity, toolLogger);
registerGetBuildStatusTool(server, mcpUnity, toolLogger);
registerGetConnectedDevicesTool(server, mcpUnity, toolLogger);

// Register XREAL tools - XR Interaction
registerSetupXrInteractionTool(server, mcpUnity, toolLogger);
registerCreateXrRigTool(server, mcpUnity, toolLogger);
registerAddXrInteractorTool(server, mcpUnity, toolLogger);
registerCreateXrUiTool(server, mcpUnity, toolLogger);

// Register XREAL tools - Performance
registerGetXrPerformanceMetricsTool(server, mcpUnity, toolLogger);
registerProfileXrSceneTool(server, mcpUnity, toolLogger);
registerCaptureXrScreenshotTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);

// Register comprehensive control resources
registerGetProfilerDataResource(server, mcpUnity, resourceLogger);
registerGetBuildStatusResource(server, mcpUnity, resourceLogger);
registerGetEditorStateResource(server, mcpUnity, resourceLogger);
registerGetProjectSettingsResource(server, mcpUnity, resourceLogger);

// Register XREAL resources
registerGetDeviceStateResource(server, mcpUnity, resourceLogger);
registerGetHandTrackingResource(server, mcpUnity, resourceLogger);
registerGetSpatialAnchorsResource(server, mcpUnity, resourceLogger);
registerGetDetectedPlanesResource(server, mcpUnity, resourceLogger);
registerGetTrackedImagesResource(server, mcpUnity, resourceLogger);
registerGetBuildSettingsResource(server, mcpUnity, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);
registerScriptWorkflowPrompt(server);
registerSceneSetupPrompt(server);
registerDebuggingWorkflowPrompt(server);
registerPerformanceOptimizationPrompt(server);
registerUiDevelopmentPrompt(server);
register2dGameDevPrompt(server);
registerWorldBuildingPrompt(server);
registerAudioDesignPrompt(server);

// Register XREAL prompts
registerXrealProjectSetupPrompt(server);
registerHandInteractionPrompt(server);
registerSpatialAnchorWorkflowPrompt(server);
registerXrealOptimizationPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();

    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('Unity MCP Server started');

    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);

    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);

  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Start the server
startServer();

// Handle shutdown
process.on('SIGINT', async () => {
  serverLogger.info('Shutting down...');
  await mcpUnity.stop();
  process.exit(0);
});

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
  serverLogger.error('Uncaught exception', error);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
});
