import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'animation';
const toolDescription = `Controls Unity Animation and Timeline systems.
Actions:
- play: Play animation on a GameObject
- stop: Stop animation
- pause: Pause animation
- sample: Sample animation at specific time
- get_clips: List animation clips on an Animator
- get_parameters: Get Animator parameters
- set_parameter: Set an Animator parameter (float, int, bool, trigger)
- get_state_info: Get current Animator state info
- crossfade: Crossfade to an animation state
- get_timeline_state: Get Timeline playback state
- set_timeline_time: Set Timeline playback position
- play_timeline: Control Timeline playback
- record_animation: Start/stop animation recording
- create_clip: Create a new animation clip`;

const paramsSchema = z.object({
  action: z.enum([
    'play', 'stop', 'pause', 'sample', 'get_clips', 'get_parameters',
    'set_parameter', 'get_state_info', 'crossfade', 'get_timeline_state',
    'set_timeline_time', 'play_timeline', 'record_animation', 'create_clip'
  ]).describe('Animation action to perform'),
  objectId: z.number().optional().describe('Instance ID of the GameObject'),
  objectPath: z.string().optional().describe('Hierarchy path of the GameObject'),
  clipName: z.string().optional().describe('Name of the animation clip'),
  stateName: z.string().optional().describe('Name of the Animator state'),
  layerIndex: z.number().optional().describe('Animator layer index (default: 0)'),
  normalizedTime: z.number().min(0).max(1).optional()
    .describe('Normalized time (0-1) for sampling or crossfade'),
  transitionDuration: z.number().optional()
    .describe('Duration of crossfade transition in seconds'),
  parameterName: z.string().optional().describe('Name of the Animator parameter'),
  parameterType: z.enum(['float', 'int', 'bool', 'trigger']).optional()
    .describe('Type of the Animator parameter'),
  parameterValue: z.any().optional().describe('Value to set for the parameter'),
  timelineTime: z.number().optional().describe('Time position for Timeline'),
  timelineAction: z.enum(['play', 'pause', 'stop', 'resume']).optional()
    .describe('Timeline playback action'),
  recordingAction: z.enum(['start', 'stop']).optional()
    .describe('Animation recording action'),
  newClipPath: z.string().optional().describe('Path for new animation clip'),
  wrapMode: z.enum(['Default', 'Once', 'Loop', 'PingPong', 'ClampForever']).optional()
    .describe('Animation wrap mode'),
  speed: z.number().optional().describe('Animation playback speed')
});

export function registerAnimationTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate target object for most actions
  const needsTarget = [
    'play', 'stop', 'pause', 'sample', 'get_clips', 'get_parameters',
    'set_parameter', 'get_state_info', 'crossfade', 'get_timeline_state',
    'set_timeline_time', 'play_timeline', 'record_animation'
  ];

  if (needsTarget.includes(action) && !params.objectId && !params.objectPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Either 'objectId' or 'objectPath' is required for ${action}`
    );
  }

  if (action === 'set_parameter' && !params.parameterName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'parameterName' is required for set_parameter"
    );
  }

  if (action === 'crossfade' && !params.stateName) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'stateName' is required for crossfade"
    );
  }

  if (action === 'create_clip' && !params.newClipPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'newClipPath' is required for create_clip"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      objectId: params.objectId,
      objectPath: params.objectPath,
      clipName: params.clipName,
      stateName: params.stateName,
      layerIndex: params.layerIndex || 0,
      normalizedTime: params.normalizedTime,
      transitionDuration: params.transitionDuration,
      parameterName: params.parameterName,
      parameterType: params.parameterType,
      parameterValue: params.parameterValue,
      timelineTime: params.timelineTime,
      timelineAction: params.timelineAction,
      recordingAction: params.recordingAction,
      newClipPath: params.newClipPath,
      wrapMode: params.wrapMode,
      speed: params.speed
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Animation action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
