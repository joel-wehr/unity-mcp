import * as z from 'zod';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { randomUUID } from 'crypto';
import axios from 'axios';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'add_external_dll';
const toolDescription = `Downloads a raw DLL (or any binary asset) from a URL and installs it under the Unity project's Assets/<destinationFolder>/<fileName>.
Use this for native plugins, NuGet DLLs, and CDN-hosted libraries that aren't published as Unity packages —
e.g. zxing.unity.dll, Newtonsoft.Json.dll, custom .so/.aar bundles. The Node MCP server downloads the
URL to a temp file and asks the Unity Editor to copy it into the Assets tree and refresh the
AssetDatabase. Use add_package (source=github) for git-hosted UPM packages instead.`;

const paramsSchema = z.object({
  url: z.string().url().describe('Direct download URL for the DLL or binary asset.'),
  destinationFolder: z.string().describe('Folder under Assets/ to place the file in. Will be created if missing. Example: "Plugins/ZXing".'),
  fileName: z.string().optional().describe('File name to save as (including extension). Defaults to the last segment of the URL.'),
  overwrite: z.boolean().default(true).describe('Overwrite an existing file at the destination. Default true.'),
});

/**
 * Registers the Add External DLL tool with the MCP server.
 * Downloads a binary on the Node side then forwards to Unity for installation.
 */
export function registerAddExternalDllTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

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
        const result = await toolHandler(mcpUnity, params, logger);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>,
  logger: Logger
): Promise<CallToolResult> {
  const { url, destinationFolder, overwrite } = params;
  let { fileName } = params;

  // Default fileName from URL path's last segment.
  if (!fileName) {
    try {
      const parsed = new URL(url);
      const lastSeg = parsed.pathname.split('/').filter(Boolean).pop();
      if (!lastSeg) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          'Could not infer fileName from URL; please pass fileName explicitly.'
        );
      }
      fileName = decodeURIComponent(lastSeg);
    } catch (e) {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        `Invalid URL: ${url}`
      );
    }
  }

  // Reject path traversal in the destination/filename — the Unity side joins these into Assets/.
  if (destinationFolder.includes('..') || path.isAbsolute(destinationFolder)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'destinationFolder must be a relative path under Assets/ (no ".." or absolute paths).'
    );
  }
  if (fileName.includes('/') || fileName.includes('\\') || fileName.includes('..')) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'fileName must be a bare file name without path separators.'
    );
  }

  // Stream the download to a temp file. We forward the temp path to Unity rather than the
  // bytes themselves so we don't have to base64-shuttle multi-megabyte DLLs over the JSON-RPC
  // WebSocket (and so users see a familiar Editor.log "Imported asset" entry).
  const tmpDir = path.join(os.tmpdir(), 'unity-mcp-dll-cache');
  await fs.promises.mkdir(tmpDir, { recursive: true });
  const tmpPath = path.join(tmpDir, `${randomUUID()}-${fileName}`);

  logger.info(`Downloading ${url} -> ${tmpPath}`);

  let sizeBytes = 0;
  try {
    const response = await axios.get<ArrayBuffer>(url, {
      responseType: 'arraybuffer',
      // Reasonable cap so a runaway URL doesn't fill the disk. ~250MB is well over any
      // single Unity-bound DLL we'd expect.
      maxContentLength: 250 * 1024 * 1024,
      maxBodyLength: 250 * 1024 * 1024,
      timeout: 180000
    });
    const buf = Buffer.from(response.data);
    sizeBytes = buf.length;
    await fs.promises.writeFile(tmpPath, buf);
  } catch (err: any) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      `Failed to download ${url}: ${err?.message ?? String(err)}`
    );
  }

  // Ask Unity to install the temp file into Assets and refresh.
  let response: any;
  try {
    response = await mcpUnity.sendRequest({
      method: toolName,
      params: {
        sourcePath: tmpPath,
        destinationFolder,
        fileName,
        overwrite,
        downloadedFromUrl: url,
        downloadedSizeBytes: sizeBytes
      }
    });
  } finally {
    // Clean up the temp file regardless of outcome.
    fs.promises.unlink(tmpPath).catch(() => { /* best effort */ });
  }

  if (!response || response.success === false) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response?.error || response?.message || 'Failed to install external DLL into Unity project'
    );
  }

  return {
    content: [{
      type: 'text' as const,
      text: JSON.stringify({ ...response, downloadedFromUrl: url, downloadedSizeBytes: sizeBytes }, null, 2)
    }]
  };
}
