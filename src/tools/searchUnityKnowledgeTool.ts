import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { spawn } from 'child_process';
import path from 'path';
import { getToolAnnotations } from '../utils/toolAnnotations.js';

// Constants for the tool
const toolName = 'search_unity_knowledge';
const toolDescription = `Search the Unity knowledge base for relevant documentation and assets.

Args:
    query: The search query string to find relevant Unity knowledge.
    filter_source: Optional filter to limit results by source type.
                  Valid values: 'api', 'manual', 'local_asset', 'reach_ui'.
                  If not provided, searches all sources.

Returns:
    Formatted string containing relevant search results with source info.`;

const paramsSchema = z.object({
  query: z.string().describe('The search query string to find relevant Unity knowledge'),
  filter_source: z.string().optional().describe("Optional filter to limit results by source type. Valid values: 'api', 'manual', 'local_asset', 'reach_ui'")
});

// Configuration for the RAG server
interface RagConfig {
  pythonPath: string;
  serverPath: string;
  dbPath: string;
}

let ragConfig: RagConfig | null = null;

/**
 * Configure the RAG server connection
 * @param config The RAG server configuration
 */
export function configureRagServer(config: RagConfig) {
  ragConfig = config;
}

/**
 * Creates and registers the Search Unity Knowledge tool with the MCP server
 * This tool allows searching the Unity knowledge base for documentation and assets
 *
 * @param server The MCP server instance to register with
 * @param logger The logger instance for diagnostic information
 */
export function registerSearchUnityKnowledgeTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(params, logger);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

/**
 * Handles searching the Unity knowledge base
 * This calls the Python RAG server via subprocess
 *
 * @param params The parameters for the tool
 * @param logger The logger instance
 * @returns A promise that resolves to the tool execution result
 */
async function toolHandler(
  params: z.infer<typeof paramsSchema>,
  logger: Logger
): Promise<CallToolResult> {
  const { query, filter_source } = params;

  // If no RAG config is set, return a helpful message
  if (!ragConfig) {
    return {
      content: [{
        type: 'text',
        text: `Unity knowledge base search is not configured.

To enable this feature, you need to:
1. Install the unity-rag Python package
2. Configure the RAG server path in the server configuration

Alternatively, you can use the MCP Inspector to manually configure the RAG server path.`
      }]
    };
  }

  try {
    // Execute the Python search script
    const result = await executePythonSearch(query, filter_source, logger);
    return {
      content: [{
        type: 'text',
        text: result
      }]
    };
  } catch (error) {
    logger.error('RAG search failed', error);
    return {
      content: [{
        type: 'text',
        text: `Search failed: ${error instanceof Error ? error.message : String(error)}`
      }]
    };
  }
}

/**
 * Execute a Python search query
 */
async function executePythonSearch(
  query: string,
  filterSource: string | undefined,
  logger: Logger
): Promise<string> {
  return new Promise((resolve, reject) => {
    if (!ragConfig) {
      reject(new Error('RAG server not configured'));
      return;
    }

    const pythonScript = `
import sys
sys.path.insert(0, '${ragConfig.serverPath.replace(/\\/g, '/')}')
from src.db_manager import UnityKB
import json

kb = UnityKB()
results = kb.search('${query.replace(/'/g, "\\'")}', limit=5${filterSource ? `, filter_source='${filterSource}'` : ''})

if not results:
    print("No results found for your query.")
else:
    formatted = []
    for r in results:
        source = r.get('source', 'unknown').upper()
        metadata = r.get('metadata', {})
        if isinstance(metadata, str):
            import json as j
import { getToolAnnotations } from "../utils/toolAnnotations.js";
            try:
                metadata = j.loads(metadata)
            except:
                metadata = {}

        if source == 'API':
            title = metadata.get('title', metadata.get('class_name', 'Unity API'))
        elif source == 'MANUAL':
            title = metadata.get('title', 'Unity Manual')
        elif source == 'LOCAL_ASSET':
            title = metadata.get('asset_name', metadata.get('file_path', 'Local Asset'))
        elif source == 'REACH_UI':
            title = metadata.get('title', 'Reach UI')
        else:
            title = metadata.get('title', 'Unknown')

        text = r.get('text', '')[:500]
        if len(r.get('text', '')) > 500:
            text += '...'

        formatted.append(f"--- [Source: {source} | Title: {title}] ---\\n{text}")

    print("\\n\\n".join(formatted))
`;

    const python = spawn(ragConfig.pythonPath, ['-c', pythonScript], {
      cwd: ragConfig.serverPath,
      env: {
        ...process.env,
        PYTHONPATH: ragConfig.serverPath
      }
    });

    let stdout = '';
    let stderr = '';

    python.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    python.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    python.on('close', (code) => {
      if (code === 0) {
        resolve(stdout.trim() || 'No results found.');
      } else {
        logger.error('Python search failed', { code, stderr });
        reject(new Error(`Search failed: ${stderr || 'Unknown error'}`));
      }
    });

    python.on('error', (err) => {
      reject(err);
    });
  });
}
