import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerWorldBuildingPrompt(server: McpServer) {
  server.prompt(
    'world_building',
    'Workflow for building 3D worlds: terrain, lighting, NavMesh, environment',
    {
      taskDescription: z.string().describe("Description of the world/level to build"),
    },
    async ({ taskDescription }) => ({
      messages: [
        {
          role: 'user' as const,
          content: {
            type: 'text' as const,
            text: `You are an expert Unity level designer with access to an MCP server connected to the Unity Editor.

When building 3D worlds, use these tools:

## Terrain Tools:
- Tool "terrain" (action: "create") — create terrain with size/resolution
- Tool "terrain" (action: "set_height") — sculpt terrain heights with brush
- Tool "terrain" (action: "flatten") — flatten to uniform height
- Tool "terrain" (action: "add_layer") — add terrain texture layers
- Tool "terrain" (action: "paint_texture") — paint textures on terrain
- Tool "terrain" (action: "add_tree") — place trees
- Tool "terrain" (action: "set_detail") — paint grass/detail
- Tool "terrain" (action: "set_settings") — LOD, draw distances

## Lighting Tools:
- Tool "lighting" (action: "get_settings") — current lighting state
- Tool "lighting" (action: "set_ambient") — ambient mode and intensity
- Tool "lighting" (action: "set_fog") — fog mode, density, distances
- Tool "lighting" (action: "set_skybox") — assign skybox material
- Tool "lighting" (action: "bake_lighting") — bake lightmaps
- Tool "lighting" (action: "render_reflection_probes") — render probes

## NavMesh Tools:
- Tool "navmesh" (action: "bake") — bake NavMesh
- Tool "navmesh" (action: "set_settings") — agent radius/height/slope
- Tool "navmesh" (action: "add_agent") — add NavMeshAgent to NPCs
- Tool "navmesh" (action: "add_obstacle") — add NavMeshObstacle
- Tool "navmesh" (action: "find_path") — test pathfinding

## Scene Structure:
- Tool "update_gameobject" — create/position GameObjects
- Tool "update_component" — add components (lights, colliders, etc.)
- Tool "material_shader" — create/assign materials
- Tool "physics" — 3D physics configuration

## World Building Workflow:
1. **Terrain**: Create terrain → add layers → sculpt heightmap → paint textures → add trees/details
2. **Lighting**: Set skybox → configure ambient → place directional light → add point/spot lights → bake
3. **Navigation**: Bake NavMesh → add agents → add obstacles → test paths
4. **Props**: Place objects → add colliders → set materials
5. **Optimization**: Set LOD distances, occlusion culling, lightmap baking

## Task:
${taskDescription}

Build the world layer by layer: terrain → lighting → navigation → props.`
          }
        }
      ]
    })
  );
}
