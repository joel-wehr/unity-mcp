import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'particle_system';
const toolDescription = `Manage Unity ParticleSystems in detail.
Actions:
- create: Create a new ParticleSystem GameObject
- get_info: Get full particle system state (main, emission, shape modules)
- set_main: Configure main module (duration, looping, lifetime, speed, size, gravity, etc.)
- set_emission: Configure emission (rate over time/distance)
- set_shape: Configure shape module (type, radius, angle, arc)
- set_renderer: Configure renderer (render mode, material, sorting order)
- play/stop/pause/restart: Playback control
- list: List all ParticleSystems in scene
- set_color_over_lifetime: Set start/end color gradient
- set_size_over_lifetime: Set start/end size curve
- set_velocity_over_lifetime: Set velocity min/max per axis
- get_modules: Check which modules are enabled`;

const paramsSchema = z.object({
  action: z.enum([
    'create', 'get_info', 'set_main', 'set_emission', 'set_shape',
    'set_renderer', 'play', 'stop', 'pause', 'restart', 'list',
    'set_color_over_lifetime', 'set_size_over_lifetime',
    'set_velocity_over_lifetime', 'get_modules'
  ]).describe('ParticleSystem action to perform'),
  objectPath: z.string().optional().describe('Hierarchy path of the ParticleSystem'),
  objectId: z.string().optional().describe('Instance ID of the ParticleSystem'),
  name: z.string().optional().describe('Name for new particle system (create)'),
  parentPath: z.string().optional().describe('Parent path (create)'),
  x: z.string().optional().describe('Position X'),
  y: z.string().optional().describe('Position Y'),
  z: z.string().optional().describe('Position Z'),
  // Main module
  duration: z.string().optional().describe('Duration in seconds'),
  looping: z.string().optional().describe('Enable looping (true/false)'),
  startLifetime: z.string().optional().describe('Start lifetime in seconds'),
  startSpeed: z.string().optional().describe('Start speed'),
  startSize: z.string().optional().describe('Start size'),
  maxParticles: z.string().optional().describe('Maximum particles'),
  gravityModifier: z.string().optional().describe('Gravity modifier'),
  playOnAwake: z.string().optional().describe('Play on awake (true/false)'),
  simulationSpace: z.string().optional().describe('local, world, or custom'),
  // Emission
  enabled: z.string().optional().describe('Enable/disable module (true/false)'),
  rateOverTime: z.string().optional().describe('Emission rate over time'),
  rateOverDistance: z.string().optional().describe('Emission rate over distance'),
  // Shape
  shapeType: z.string().optional().describe('Shape type (Sphere, Hemisphere, Cone, Box, Circle, etc.)'),
  radius: z.string().optional().describe('Shape radius'),
  angle: z.string().optional().describe('Cone angle'),
  arc: z.string().optional().describe('Shape arc'),
  // Renderer
  renderMode: z.string().optional().describe('Billboard, Stretch, HorizontalBillboard, VerticalBillboard, Mesh'),
  materialPath: z.string().optional().describe('Path to material asset'),
  sortingOrder: z.string().optional().describe('Sorting order'),
  // Color over lifetime
  startColor: z.string().optional().describe('Start color JSON: {"r":1,"g":0,"b":0,"a":1}'),
  endColor: z.string().optional().describe('End color JSON: {"r":0,"g":0,"b":1,"a":0}'),
  // Size over lifetime
  startSizeOL: z.string().optional().describe('Start size for size-over-lifetime curve'),
  endSize: z.string().optional().describe('End size for size-over-lifetime curve'),
  // Velocity over lifetime
  xMin: z.string().optional().describe('Velocity X min'),
  xMax: z.string().optional().describe('Velocity X max'),
  yMin: z.string().optional().describe('Velocity Y min'),
  yMax: z.string().optional().describe('Velocity Y max'),
  zMin: z.string().optional().describe('Velocity Z min'),
  zMax: z.string().optional().describe('Velocity Z max')
});

export function registerParticleSystemTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
            response.message || `ParticleSystem action '${params.action}' failed`
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
