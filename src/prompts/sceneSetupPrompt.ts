import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import * as z from "zod";

export function registerSceneSetupPrompt(server: McpServer) {
  server.prompt(
    'scene_setup',
    'Workflow for setting up and configuring Unity scenes',
    {
      sceneName: z.string().describe("Name of the scene to set up"),
      sceneDescription: z.string().optional().describe("Description of what the scene should contain"),
    },
    async ({ sceneName, sceneDescription }) => ({
      messages: [
        {
          role: 'user',
          content: {
            type: 'text',
            text: `You are an expert Unity developer with access to an MCP server connected to the Unity Editor.

When setting up a Unity scene, follow this workflow:

Available Tools:
- Resource "unity://scenes_hierarchy" to view current scene contents
- Tool "create_scene" to create a new scene
- Tool "load_scene" to load an existing scene
- Tool "update_gameobject" to create/configure GameObjects
- Tool "update_component" to add/configure components
- Tool "lighting" to configure lighting settings
- Tool "material_shader" to create and assign materials
- Tool "physics" to configure physics settings
- Tool "editor_control" (action: "set_scene_view") to adjust the Scene view camera
- Tool "execute_code" for complex setup operations

Workflow:
1. Check if scene "${sceneName}" exists using scene resources
2. Create or load the scene as needed
3. Set up the basic structure:
   - Camera (Main Camera with proper settings)
   - Lighting (Directional Light, ambient settings)
   - Environment (ground plane, skybox, fog)
4. Add game-specific objects based on: ${sceneDescription || 'the scene requirements'}
5. Configure components on all objects
6. Set up lighting using the "lighting" tool
7. Verify the scene hierarchy
8. Save the scene

Scene Setup Best Practices:
- Always have a Main Camera tagged "MainCamera"
- Set up proper lighting before adding visual elements
- Use layers for organization and physics filtering
- Add a Canvas (Screen Space - Overlay) for UI elements
- Configure CanvasScaler for mobile (1080x1920, ScaleWithScreenSize)
- Set appropriate camera clear flags and background color`
          }
        },
        {
          role: 'user',
          content: {
            type: 'text',
            text: `Set up the scene "${sceneName}" with the following requirements: ${sceneDescription || 'standard game scene'}`
          }
        }
      ]
    })
  );
}
