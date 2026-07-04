import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

const toolName = 'audio_mixer';
const toolDescription = `Manage Unity AudioMixers and AudioSources.
Actions:
- list: List all AudioMixer assets in the project
- get_groups: Get mixer groups (channels)
- get_snapshots: Get mixer snapshots
- set_float: Set an exposed parameter value
- get_float: Get an exposed parameter value
- transition_snapshot: Transition to a named snapshot over duration
- get_exposed_parameters: List all exposed parameters with current values
- get_audio_sources: List all AudioSource components in the scene`;

const paramsSchema = z.object({
  action: z.enum([
    'list', 'get_groups', 'get_snapshots', 'set_float', 'get_float',
    'transition_snapshot', 'get_exposed_parameters', 'get_audio_sources'
  ]).describe('AudioMixer action to perform'),
  mixerPath: z.string().optional().describe('Asset path to the AudioMixer'),
  mixerName: z.string().optional().describe('Name of the AudioMixer to find'),
  parameterName: z.string().optional().describe('Name of the exposed parameter'),
  value: z.string().optional().describe('Float value to set'),
  snapshotName: z.string().optional().describe('Name of the snapshot to transition to'),
  duration: z.string().optional().describe('Transition duration in seconds (default: 1)')
});

export function registerAudioMixerTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.registerTool(
    toolName,
    {
      description: toolDescription,
      inputSchema: paramsSchema.shape,
      annotations: getToolAnnotations(toolName),
    },
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: toolName,
          params
        });

        if (!response.success) {
          throw new McpUnityError(
            ErrorType.TOOL_EXECUTION,
            response.message || `AudioMixer action '${params.action}' failed`
          );
        }

        return {
          content: [{
            type: "text" as const,
            text: JSON.stringify(response, null, 2)
          }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}
