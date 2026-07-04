import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'asset_store';
const toolDescription = `Manages Unity Asset Store assets and Package Manager integration.

Actions:
- list_my_assets: List all purchased/acquired assets from your Asset Store account
- search_my_assets: Search your purchased assets by name
- get_asset_details: Get details about a specific asset
- download_asset: Download an asset to the local cache
- import_asset: Import a downloaded asset into the project
- download_and_import: Download and import in one step
- get_download_progress: Check download progress
- cancel_download: Cancel an ongoing download
- list_cached_assets: List assets already downloaded to local cache
- clear_cache: Clear the asset cache
- refresh_my_assets: Refresh the list of purchased assets from server
- search_store: Search the public Unity Asset Store catalog (no auth required)

Note: Most actions require Unity 2020.1+ and being logged into Unity Hub/Editor.
search_store and list_cached_assets work without authentication.
Assets must be purchased through the Asset Store website before downloading.`;

const paramsSchema = z.object({
  action: z.enum([
    'list_my_assets', 'search_my_assets', 'get_asset_details',
    'download_asset', 'import_asset', 'download_and_import',
    'get_download_progress', 'cancel_download', 'list_cached_assets',
    'clear_cache', 'refresh_my_assets', 'search_store'
  ]).describe('Asset Store action to perform'),
  assetId: z.string().optional()
    .describe('Asset Store asset ID or package ID'),
  assetName: z.string().optional()
    .describe('Asset name for searching'),
  searchQuery: z.string().optional()
    .describe('Search query for filtering assets'),
  category: z.enum([
    'All', '3D', '2D', 'Add-Ons', 'Audio', 'Essentials', 'Templates',
    'Tools', 'VFX', 'Sale'
  ]).optional().describe('Filter by category'),
  importOptions: z.object({
    includeAll: z.boolean().optional().describe('Import all files (default: true)'),
    includePaths: z.array(z.string()).optional().describe('Specific paths to import'),
    excludePaths: z.array(z.string()).optional().describe('Paths to exclude from import'),
    interactive: z.boolean().optional().describe('Show import dialog (default: false)')
  }).optional().describe('Options for importing assets'),
  page: z.number().min(1).optional().describe('Page number for pagination'),
  pageSize: z.number().min(1).max(100).optional().describe('Results per page (default: 50)'),
  sortBy: z.enum([
    'relevance', 'popularity', 'name', 'price', 'rating', 'release_date'
  ]).optional().describe('Sort order for search_store results (default: relevance)'),
  free_only: z.boolean().optional().describe('Only return free assets in search_store results')
});

export function registerAssetStoreTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate required parameters
  const needsAssetId = ['get_asset_details', 'download_asset', 'import_asset', 'download_and_import', 'cancel_download'];
  if (needsAssetId.includes(action) && !params.assetId && !params.assetName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Either 'assetId' or 'assetName' is required for ${action}`
    );
  }

  if (action === 'search_my_assets' && !params.searchQuery) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'searchQuery' is required for search_my_assets"
    );
  }

  if (action === 'search_store' && !params.searchQuery) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'searchQuery' is required for search_store"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      assetId: params.assetId,
      assetName: params.assetName,
      searchQuery: params.searchQuery,
      category: params.category || 'All',
      importOptions: params.importOptions || { includeAll: true, interactive: false },
      page: params.page || 1,
      pageSize: params.pageSize || 50,
      sortBy: params.sortBy || 'relevance',
      free_only: params.free_only || false
    }
  });

  if (!response.success) {
    // Check for common errors
    if (response.message?.includes('not logged in') || response.message?.includes('authentication')) {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        'Not logged into Unity account. Please log in via Unity Hub or Editor first.'
      );
    }
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Asset Store action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
