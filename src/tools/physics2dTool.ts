import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'physics2d';
const toolDescription = `Manage Unity 2D Physics: rigidbodies, colliders, raycasts, joints, effectors.
Actions:
- get_settings: Get Physics2D global settings (gravity, iterations, etc.)
- set_settings: Modify Physics2D settings
- raycast: Cast a 2D ray and return hit info
- overlap_circle: Find all colliders within a circle
- overlap_box: Find all colliders within a box
- get_rigidbodies: List all Rigidbody2D components in scene
- set_rigidbody: Add/configure Rigidbody2D on a GameObject
- add_force: Apply force to a Rigidbody2D
- get_colliders: List all Collider2D components in scene
- add_collider: Add a 2D collider to a GameObject
- get_joints: List all Joint2D components in scene
- get_effectors: List all Effector2D components in scene
- get_layers: Get 2D collision layer matrix
- set_layer_collision: Enable/disable collision between two layers`;

const paramsSchema = z.object({
  action: z.enum([
    'get_settings', 'set_settings', 'raycast', 'overlap_circle', 'overlap_box',
    'get_rigidbodies', 'set_rigidbody', 'add_force', 'get_colliders',
    'add_collider', 'get_joints', 'get_effectors', 'get_layers', 'set_layer_collision'
  ]).describe('Physics2D action to perform'),
  objectPath: z.string().optional().describe('Hierarchy path of a GameObject'),
  objectId: z.string().optional().describe('Instance ID of a GameObject'),
  // Settings
  gravityX: z.string().optional().describe('Gravity X component'),
  gravityY: z.string().optional().describe('Gravity Y component'),
  defaultContactOffset: z.string().optional().describe('Default contact offset'),
  velocityIterations: z.string().optional().describe('Velocity solver iterations'),
  positionIterations: z.string().optional().describe('Position solver iterations'),
  queriesHitTriggers: z.string().optional().describe('Whether queries hit triggers (true/false)'),
  queriesStartInColliders: z.string().optional().describe('Whether queries start in colliders (true/false)'),
  // Raycast
  originX: z.string().optional().describe('Ray origin X'),
  originY: z.string().optional().describe('Ray origin Y'),
  dirX: z.string().optional().describe('Ray direction X'),
  dirY: z.string().optional().describe('Ray direction Y'),
  distance: z.string().optional().describe('Max ray distance'),
  // Overlap
  x: z.string().optional().describe('Center X'),
  y: z.string().optional().describe('Center Y'),
  radius: z.string().optional().describe('Circle radius'),
  sizeX: z.string().optional().describe('Box half-width'),
  sizeY: z.string().optional().describe('Box half-height'),
  angle: z.string().optional().describe('Box rotation angle'),
  // Rigidbody
  bodyType: z.string().optional().describe('dynamic, kinematic, or static'),
  mass: z.string().optional().describe('Rigidbody mass'),
  gravityScale: z.string().optional().describe('Gravity scale'),
  linearDamping: z.string().optional().describe('Linear damping'),
  angularDamping: z.string().optional().describe('Angular damping'),
  simulated: z.string().optional().describe('Whether rigidbody is simulated (true/false)'),
  // Force
  forceX: z.string().optional().describe('Force X component'),
  forceY: z.string().optional().describe('Force Y component'),
  forceMode: z.string().optional().describe('Force or Impulse'),
  // Collider
  colliderType: z.string().optional().describe('box, circle, capsule, polygon, or edge'),
  isTrigger: z.string().optional().describe('Whether collider is a trigger (true/false)'),
  // Layers
  layer1: z.string().optional().describe('First layer name or index'),
  layer2: z.string().optional().describe('Second layer name or index'),
  ignore: z.string().optional().describe('Ignore collision between layers (true/false)')
});

export function registerPhysics2dTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
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
            response.message || `Physics2D action '${params.action}' failed`
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
