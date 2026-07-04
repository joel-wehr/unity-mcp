import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { ResourceTemplate } from '@modelcontextprotocol/sdk/server/mcp.js';

const resourceUri = 'unity://profiler/{dataType}';
const resourceName = 'Unity Profiler Data';
const resourceDescription = `Real-time profiler data from Unity.
Data types: frame_timing, memory, rendering, cpu, gc_allocs, physics
Example URIs:
- unity://profiler/frame_timing - Frame timing data
- unity://profiler/memory - Memory usage breakdown
- unity://profiler/rendering - Draw calls, triangles, batches
- unity://profiler/cpu - CPU time by area
- unity://profiler/gc_allocs - GC allocation data
- unity://profiler/physics - Physics stats`;

export function registerGetProfilerDataResource(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering resource: ${resourceUri}`);

  const resourceTemplate = new ResourceTemplate(resourceUri, {
    list: async () => {
      return {
        resources: [
          {
            uri: 'unity://profiler/frame_timing',
            name: 'Frame Timing Data',
            description: 'Frame times, FPS, and frame budget utilization',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://profiler/memory',
            name: 'Memory Usage',
            description: 'Memory breakdown by category (Native, Managed, Graphics, etc.)',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://profiler/rendering',
            name: 'Rendering Stats',
            description: 'Draw calls, triangles, vertices, batches, set pass calls',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://profiler/cpu',
            name: 'CPU Usage',
            description: 'CPU time breakdown by Unity area',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://profiler/gc_allocs',
            name: 'GC Allocations',
            description: 'Garbage collection allocations this frame',
            mimeType: 'application/json'
          },
          {
            uri: 'unity://profiler/physics',
            name: 'Physics Stats',
            description: 'Active rigidbodies, contacts, collision pairs',
            mimeType: 'application/json'
          }
        ]
      };
    }
  });

  server.resource(
    resourceName,
    resourceTemplate,
    { description: resourceDescription },
    async (uri, variables) => {
      logger.info(`Fetching profiler data: ${uri.href}`);

      const dataType = variables.dataType as string;
      if (!dataType) {
        throw new McpUnityError(
          ErrorType.VALIDATION,
          "Data type is required in URI"
        );
      }

      const response = await mcpUnity.sendRequest({
        method: 'get_profiler_data',
        params: { dataType }
      });

      if (!response.success) {
        throw new McpUnityError(
          ErrorType.RESOURCE_FETCH,
          response.message || `Failed to fetch profiler data: ${dataType}`
        );
      }

      return {
        contents: [{
          uri: uri.href,
          mimeType: 'application/json',
          text: JSON.stringify(response, null, 2)
        }]
      };
    }
  );
}
