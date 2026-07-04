import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { getToolAnnotations } from "../utils/toolAnnotations.js";

// Constants for the tool
const toolName = 'physics';
const toolDescription = `Unity Physics system control and queries.
Actions:
- raycast: Perform a raycast and return hit info
- raycast_all: Raycast returning all hits
- spherecast: Sphere-based raycast
- boxcast: Box-based raycast
- overlap_sphere: Find colliders in a sphere
- overlap_box: Find colliders in a box
- simulate: Step physics simulation manually
- get_contacts: Get contact points between colliders
- set_gravity: Set physics gravity
- get_layer_collision: Get layer collision matrix
- set_layer_collision: Set layer collision matrix
- add_force: Add force to a Rigidbody
- add_torque: Add torque to a Rigidbody
- set_velocity: Set Rigidbody velocity
- get_rigidbody_state: Get Rigidbody physics state`;

const paramsSchema = z.object({
  action: z.enum([
    'raycast', 'raycast_all', 'spherecast', 'boxcast',
    'overlap_sphere', 'overlap_box', 'simulate', 'get_contacts',
    'set_gravity', 'get_layer_collision', 'set_layer_collision',
    'add_force', 'add_torque', 'set_velocity', 'get_rigidbody_state'
  ]).describe('Physics action to perform'),
  origin: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Ray/query origin point'),
  direction: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Ray direction (will be normalized)'),
  maxDistance: z.number().optional().describe('Maximum raycast distance'),
  radius: z.number().optional().describe('Radius for sphere cast/overlap'),
  halfExtents: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Half extents for box cast/overlap'),
  orientation: z.object({
    x: z.number(), y: z.number(), z: z.number(), w: z.number()
  }).optional().describe('Rotation quaternion for box queries'),
  layerMask: z.number().optional().describe('Layer mask for filtering (default: all layers)'),
  queryTriggers: z.boolean().optional().describe('Include trigger colliders'),
  objectId: z.number().optional().describe('Instance ID of the Rigidbody object'),
  objectPath: z.string().optional().describe('Hierarchy path of the Rigidbody object'),
  force: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Force vector to apply'),
  forceMode: z.enum(['Force', 'Impulse', 'VelocityChange', 'Acceleration']).optional()
    .describe('Force application mode'),
  velocity: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Velocity vector to set'),
  angularVelocity: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('Angular velocity to set'),
  gravity: z.object({
    x: z.number(), y: z.number(), z: z.number()
  }).optional().describe('New gravity vector'),
  layer1: z.number().optional().describe('First layer index for collision matrix'),
  layer2: z.number().optional().describe('Second layer index for collision matrix'),
  collide: z.boolean().optional().describe('Whether layers should collide'),
  simulationSteps: z.number().min(1).max(100).optional()
    .describe('Number of physics simulation steps'),
  maxResults: z.number().min(1).max(100).optional()
    .describe('Maximum results for multi-hit queries')
});

export function registerPhysicsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

  // Validate required parameters for ray/cast operations
  const rayActions = ['raycast', 'raycast_all', 'spherecast', 'boxcast'];
  if (rayActions.includes(action) && (!params.origin || !params.direction)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameters 'origin' and 'direction' are required for ${action}`
    );
  }

  // Validate sphere-specific parameters
  if ((action === 'spherecast' || action === 'overlap_sphere') && !params.radius) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'radius' is required for ${action}`
    );
  }

  // Validate box-specific parameters
  if ((action === 'boxcast' || action === 'overlap_box') && !params.halfExtents) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Parameter 'halfExtents' is required for ${action}`
    );
  }

  // Validate overlap queries
  if (action === 'overlap_sphere' && !params.origin) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'origin' is required for overlap_sphere"
    );
  }

  // Validate rigidbody operations
  const rigidbodyActions = ['add_force', 'add_torque', 'set_velocity', 'get_rigidbody_state'];
  if (rigidbodyActions.includes(action) && !params.objectId && !params.objectPath) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      `Either 'objectId' or 'objectPath' is required for ${action}`
    );
  }

  if (action === 'add_force' && !params.force) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'force' is required for add_force"
    );
  }

  if (action === 'set_gravity' && !params.gravity) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameter 'gravity' is required for set_gravity"
    );
  }

  if (action === 'set_layer_collision' && (params.layer1 === undefined || params.layer2 === undefined || params.collide === undefined)) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Parameters 'layer1', 'layer2', and 'collide' are required for set_layer_collision"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      ...params,
      maxDistance: params.maxDistance || 1000,
      maxResults: params.maxResults || 10,
      queryTriggers: params.queryTriggers !== false
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Physics action '${action}' failed`
    );
  }

  return {
    content: [{
      type: "text" as const,
      text: JSON.stringify(response, null, 2)
    }]
  };
}
